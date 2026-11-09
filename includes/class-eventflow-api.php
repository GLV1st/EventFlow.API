<?php
if (!defined('ABSPATH')) {
    exit;
}

final class EventFlow_WC_API
{
    private const OPTION = 'eventflow_wc_connection';

    public static function connection(): array
    {
        $value = get_option(self::OPTION, []);
        return is_array($value) ? $value : [];
    }

    public static function save_connection(array $connection): void
    {
        update_option(self::OPTION, $connection, false);
    }

    public static function api_base_url(): string
    {
        $connection = self::connection();
        $url = isset($connection['api_url']) ? trim((string) $connection['api_url']) : '';
        return untrailingslashit($url);
    }

    public static function is_connected(): bool
    {
        $c = self::connection();
        return !empty($c['tenant_id']) && !empty($c['client_id']) && !empty($c['client_secret']);
    }

    public static function register_site(string $api_url, string $code): array|WP_Error
    {
        $api_url = untrailingslashit(esc_url_raw(trim($api_url)));
        if (!$api_url || !wp_http_validate_url($api_url)) {
            return new WP_Error('eventflow_invalid_url', __('Enter a valid EventFlow API URL.', 'eventflow-woocommerce'));
        }
        if (stripos($api_url, 'https://') !== 0 && stripos($api_url, 'http://localhost') !== 0) {
            return new WP_Error('eventflow_https_required', __('The EventFlow API URL must use HTTPS.', 'eventflow-woocommerce'));
        }

        $response = wp_remote_post($api_url . '/auth/register', [
            'timeout' => 30,
            'headers' => ['Content-Type' => 'application/json', 'Accept' => 'application/json'],
            'body' => wp_json_encode([
                'code' => sanitize_text_field($code),
                'siteUrl' => home_url('/'),
                'siteName' => get_bloginfo('name'),
            ]),
        ]);

        if (is_wp_error($response)) {
            return $response;
        }

        $status = wp_remote_retrieve_response_code($response);
        $body = json_decode(wp_remote_retrieve_body($response), true);

        if ($status !== 200 || !is_array($body) || empty($body['clientId']) || empty($body['clientSecret']) || empty($body['tenantId'])) {
            $message = is_array($body) && !empty($body['message']) ? sanitize_text_field($body['message']) : __('Registration failed. Check the code and API URL.', 'eventflow-woocommerce');
            return new WP_Error('eventflow_registration_failed', $message);
        }

        self::save_connection([
            'api_url' => $api_url,
            'tenant_id' => sanitize_text_field($body['tenantId']),
            'client_id' => sanitize_text_field($body['clientId']),
            'client_secret' => sanitize_text_field($body['clientSecret']),
            'registered_at' => current_time('mysql'),
        ]);

        delete_transient('eventflow_wc_access_token');
        return ['message' => __('This website is now registered with EventFlow.', 'eventflow-woocommerce')];
    }

    public static function access_token(): string|WP_Error
    {
        $cached = get_transient('eventflow_wc_access_token');
        if (is_string($cached) && $cached !== '') {
            return $cached;
        }

        $c = self::connection();
        if (empty($c['api_url']) || empty($c['client_id']) || empty($c['client_secret'])) {
            return new WP_Error('eventflow_not_connected', __('Connect this website to EventFlow first.', 'eventflow-woocommerce'));
        }

        $response = wp_remote_post(untrailingslashit($c['api_url']) . '/auth/token', [
            'timeout' => 20,
            'headers' => ['Content-Type' => 'application/json', 'Accept' => 'application/json'],
            'body' => wp_json_encode([
                'clientId' => $c['client_id'],
                'clientSecret' => $c['client_secret'],
            ]),
        ]);

        if (is_wp_error($response)) {
            return $response;
        }

        $status = wp_remote_retrieve_response_code($response);
        $body = json_decode(wp_remote_retrieve_body($response), true);

        if ($status !== 200 || !is_array($body) || empty($body['accessToken'])) {
            $message = is_array($body) && !empty($body['message']) ? sanitize_text_field($body['message']) : __('Could not obtain an EventFlow access token.', 'eventflow-woocommerce');
            return new WP_Error('eventflow_token_failed', $message);
        }

        $expires = isset($body['expiresIn']) ? max(60, (int) $body['expiresIn'] - 60) : 840;
        set_transient('eventflow_wc_access_token', sanitize_text_field($body['accessToken']), $expires);

        return (string) $body['accessToken'];
    }

    public static function request(string $method, string $path, ?array $data = null): array|WP_Error
    {
        $token = self::access_token();
        if (is_wp_error($token)) {
            return $token;
        }

        $args = [
            'method' => strtoupper($method),
            'timeout' => 30,
            'headers' => [
                'Authorization' => 'Bearer ' . $token,
                'Accept' => 'application/json',
                'Content-Type' => 'application/json',
            ],
        ];

        if ($data !== null) {
            $args['body'] = wp_json_encode($data);
        }

        $response = wp_remote_request(self::api_base_url() . $path, $args);
        if (is_wp_error($response)) {
            return $response;
        }

        $status = wp_remote_retrieve_response_code($response);
        $body_raw = wp_remote_retrieve_body($response);
        $body = $body_raw !== '' ? json_decode($body_raw, true) : [];

        if ($status < 200 || $status >= 300) {
            $message = is_array($body) && !empty($body['message']) ? sanitize_text_field($body['message']) : sprintf(__('EventFlow API returned HTTP %d.', 'eventflow-woocommerce'), $status);
            return new WP_Error('eventflow_api_error', $message, ['status' => $status, 'body' => $body]);
        }

        return is_array($body) ? $body : [];
    }

    public static function disconnect(): void
    {
        delete_option(self::OPTION);
        delete_transient('eventflow_wc_access_token');
    }
}
