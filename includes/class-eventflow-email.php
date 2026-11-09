<?php
if (!defined('ABSPATH')) {
    exit;
}

final class EventFlow_WC_Email
{
    public static function send(WC_Order $order, array $tickets, bool $reminder = false): bool
    {
        $email = $order->get_billing_email();
        if (!$email || empty($tickets)) {
            return false;
        }

        $settings = get_option('eventflow_wc_display', []);
        $settings = is_array($settings) ? $settings : [];

        $primary = !empty($settings['primary_colour']) ? sanitize_hex_color($settings['primary_colour']) : '#1f4d3a';
        $accent = !empty($settings['accent_colour']) ? sanitize_hex_color($settings['accent_colour']) : '#d4a72c';
        $primary = $primary ?: '#1f4d3a';
        $accent = $accent ?: '#d4a72c';

        $logo_id = get_theme_mod('custom_logo');
        $logo_url = $logo_id ? wp_get_attachment_image_url($logo_id, 'full') : '';
        $site_name = get_bloginfo('name');
        $site_url = home_url('/');
        $customer_name = $order->get_formatted_billing_full_name();

        $subject = $reminder
            ? sprintf(__('Your tickets for %s are coming up', 'eventflow-woocommerce'), $site_name)
            : sprintf(__('Your event tickets - Order #%s', 'eventflow-woocommerce'), $order->get_order_number());

        $heading = $reminder ? __('Your event is coming up soon!', 'eventflow-woocommerce') : __('Thank you for your purchase!', 'eventflow-woocommerce');
        $intro = $reminder
            ? __('Your event is coming up soon, so we are sending your tickets again to make sure you have them ready.', 'eventflow-woocommerce')
            : __('Thank you for your purchase. Your tickets are shown below.', 'eventflow-woocommerce');

        $message = '<div style="margin:0;padding:30px 10px;background:#f2f2f2;font-family:Arial,Helvetica,sans-serif;">';
        $message .= '<div style="max-width:650px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 3px 15px rgba(0,0,0,.12);">';
        $message .= '<div style="background:' . esc_attr($primary) . ';padding:28px 20px;text-align:center;">';

        if ($logo_url) {
            $message .= '<img src="' . esc_url($logo_url) . '" alt="' . esc_attr($site_name) . '" style="display:block;width:auto;max-width:300px;max-height:90px;height:auto;margin:0 auto 16px;">';
        } else {
            $message .= '<div style="color:#fff;font-size:28px;font-weight:bold;margin-bottom:10px;">' . esc_html($site_name) . '</div>';
        }

        $message .= '<div style="color:#fff;font-size:13px;font-weight:bold;letter-spacing:3px;text-transform:uppercase;">' . esc_html__('Your event tickets', 'eventflow-woocommerce') . '</div></div>';
        $message .= '<div style="padding:30px 30px 10px;color:#222;">';
        $message .= '<h2 style="margin:0 0 15px;font-size:26px;line-height:1.2;color:#222;">' . esc_html($heading) . '</h2>';
        $message .= '<p style="font-size:16px;line-height:1.6;">' . sprintf(esc_html__('Hello %s,', 'eventflow-woocommerce'), esc_html($customer_name)) . '</p>';
        $message .= '<p style="font-size:16px;line-height:1.6;">' . esc_html($intro) . '</p>';
        $message .= '<div style="margin:22px 0;padding:18px;background:#f8f4df;border-left:5px solid ' . esc_attr($accent) . ';border-radius:6px;"><strong>' . esc_html__('Please keep these tickets available when attending the event.', 'eventflow-woocommerce') . '</strong> ' . esc_html__('Each barcode will be scanned on arrival.', 'eventflow-woocommerce') . '</div>';
        $message .= '<p style="text-align:center;font-size:15px;">' . esc_html__('For event information and updates, please visit:', 'eventflow-woocommerce') . '<br><a href="' . esc_url($site_url) . '" style="color:' . esc_attr($primary) . ';font-weight:bold;text-decoration:none;">' . esc_html($site_url) . '</a></p></div>';

        foreach ($tickets as $ticket) {
            if (empty($ticket['ticket_number'])) {
                continue;
            }
            $number = (string) $ticket['ticket_number'];
            $type = !empty($ticket['ticket_type']) ? (string) $ticket['ticket_type'] : __('Ticket', 'eventflow-woocommerce');
            $event = !empty($ticket['event_name']) ? (string) $ticket['event_name'] : $site_name;
            $barcode_url = add_query_arg([
                'bcid' => 'code128',
                'text' => $number,
                'scale' => 3,
                'height' => 12,
                'includetext' => '',
            ], 'https://bwipjs-api.metafloor.com/');

            $message .= '<div style="margin:25px 30px;border:2px solid ' . esc_attr($primary) . ';border-radius:14px;overflow:hidden;background:#fff;">';
            $message .= '<div style="background:' . esc_attr($primary) . ';padding:14px 20px;text-align:center;color:#fff;font-weight:bold;letter-spacing:2px;">' . esc_html($site_name) . '</div>';
            $message .= '<div style="padding:25px 20px;text-align:center;">';
            $message .= '<div style="font-size:22px;font-weight:bold;color:#222;margin-bottom:18px;">' . esc_html($event) . '</div>';
            $message .= '<div style="display:inline-block;padding:8px 18px;background:' . esc_attr($accent) . ';color:#222;border-radius:30px;font-size:14px;font-weight:bold;text-transform:uppercase;margin-bottom:22px;">' . esc_html($type) . '</div>';
            $message .= '<div style="font-size:12px;color:#777;text-transform:uppercase;letter-spacing:2px;">' . esc_html__('Ticket number', 'eventflow-woocommerce') . '</div>';
            $message .= '<div style="font-size:25px;font-weight:bold;color:#222;margin:5px 0 20px;">' . esc_html($number) . '</div>';
            $message .= '<div style="border-top:1px dashed #ccc;margin:20px 0;"></div>';
            $message .= '<img src="' . esc_url($barcode_url) . '" alt="' . esc_attr__('Ticket barcode', 'eventflow-woocommerce') . '" style="display:block;width:100%;max-width:450px;height:auto;margin:0 auto;">';
            $message .= '<p style="font-size:13px;color:#666;">' . esc_html__('This barcode will be scanned when you arrive.', 'eventflow-woocommerce') . '</p>';
            $message .= '</div></div>';
        }

        $message .= '<div style="padding:10px 30px 30px;text-align:center;color:#777;font-size:12px;">';
        $message .= '<p>' . esc_html__('Please keep your tickets available when attending the event.', 'eventflow-woocommerce') . '</p>';
        $message .= '<p>' . sprintf(esc_html__('Order #%s', 'eventflow-woocommerce'), esc_html($order->get_order_number())) . '</p>';
        $message .= '<a href="' . esc_url($site_url) . '" style="color:' . esc_attr($primary) . ';font-weight:bold;text-decoration:none;">' . esc_html($site_name) . '</a>';
        $message .= '</div></div></div>';

        return (bool) wp_mail($email, $subject, $message, ['Content-Type: text/html; charset=UTF-8']);
    }
}
