<?php
if (!defined('ABSPATH')) {
    exit;
}

/**
 * WordPress administration screens for EventFlow.
 */
final class EventFlow_WC_Admin
{
    private static ?self $instance = null;

    public static function instance(): self
    {
        if (self::$instance === null) {
            self::$instance = new self();
        }

        return self::$instance;
    }

    private function __construct()
    {
        add_action('admin_menu', [$this, 'admin_menu']);
        add_action('admin_post_eventflow_register_site', [$this, 'register_site']);
        add_action('admin_post_eventflow_disconnect_site', [$this, 'disconnect_site']);
        add_action('admin_post_eventflow_save_display', [$this, 'save_display']);
        add_action('admin_post_eventflow_send_reminder', [$this, 'send_reminder']);
    }

    public function admin_menu(): void
    {
        add_submenu_page(
            'woocommerce',
            __('EventFlow', 'eventflow-woocommerce'),
            __('EventFlow', 'eventflow-woocommerce'),
            'manage_woocommerce',
            'eventflow',
            [$this, 'render_settings_page']
        );

        add_submenu_page(
            'woocommerce',
            __('EventFlow Reminders', 'eventflow-woocommerce'),
            __('EventFlow Reminders', 'eventflow-woocommerce'),
            'manage_woocommerce',
            'eventflow-reminders',
            [$this, 'render_reminders_page']
        );
    }

    public function render_settings_page(): void
    {
        if (!current_user_can('manage_woocommerce')) {
            return;
        }

        $connection = EventFlow_WC_API::connection();
        $display = get_option('eventflow_wc_display', []);
        $display = is_array($display) ? $display : [];
        $connected = EventFlow_WC_API::is_connected();
        ?>
        <div class="wrap">
            <h1><?php esc_html_e('EventFlow for WooCommerce', 'eventflow-woocommerce'); ?></h1>
            <?php $this->render_notice(); ?>

            <div class="card" style="max-width:760px;padding:20px;margin-top:20px;">
                <h2><?php esc_html_e('Website connection', 'eventflow-woocommerce'); ?></h2>
                <?php if ($connected) : ?>
                    <p><strong style="color:#16803c;"><?php esc_html_e('Connected to EventFlow', 'eventflow-woocommerce'); ?></strong></p>
                    <p><?php esc_html_e('This website has been registered with the central EventFlow service.', 'eventflow-woocommerce'); ?></p>
                    <p><strong><?php esc_html_e('API URL:', 'eventflow-woocommerce'); ?></strong> <?php echo esc_html($connection['api_url'] ?? ''); ?></p>
                    <p><strong><?php esc_html_e('Tenant ID:', 'eventflow-woocommerce'); ?></strong> <?php echo esc_html($connection['tenant_id'] ?? ''); ?></p>
                    <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>" onsubmit="return confirm('<?php echo esc_js(__('Disconnect this website from EventFlow?', 'eventflow-woocommerce')); ?>');">
                        <input type="hidden" name="action" value="eventflow_disconnect_site">
                        <?php wp_nonce_field('eventflow_disconnect_site'); ?>
                        <?php submit_button(__('Disconnect website', 'eventflow-woocommerce'), 'delete'); ?>
                    </form>
                <?php else : ?>
                    <p><?php esc_html_e('Enter your EventFlow API address and the one-time registration code generated in the EventFlow Azure Function App.', 'eventflow-woocommerce'); ?></p>
                    <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>">
                        <input type="hidden" name="action" value="eventflow_register_site">
                        <?php wp_nonce_field('eventflow_register_site'); ?>
                        <table class="form-table" role="presentation">
                            <tr>
                                <th scope="row"><label for="eventflow_api_url"><?php esc_html_e('EventFlow API URL', 'eventflow-woocommerce'); ?></label></th>
                                <td><input name="api_url" id="eventflow_api_url" type="url" class="regular-text" required value="<?php echo esc_attr($connection['api_url'] ?? 'https://eeventflowapi.azurewebsites.net/api'); ?>" placeholder="https://your-function-app.azurewebsites.net/api"></td>
                            </tr>
                            <tr>
                                <th scope="row"><label for="eventflow_registration_code"><?php esc_html_e('Registration code', 'eventflow-woocommerce'); ?></label></th>
                                <td><input name="registration_code" id="eventflow_registration_code" type="text" class="regular-text" required autocomplete="off"></td>
                            </tr>
                        </table>
                        <?php submit_button(__('Register this website', 'eventflow-woocommerce')); ?>
                    </form>
                <?php endif; ?>
            </div>

            <div class="card" style="max-width:760px;padding:20px;margin-top:20px;">
                <h2><?php esc_html_e('Ticket email appearance', 'eventflow-woocommerce'); ?></h2>
                <p><?php esc_html_e('Ticket emails use your WordPress site name and custom logo automatically. You can optionally change the colours below.', 'eventflow-woocommerce'); ?></p>
                <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>">
                    <input type="hidden" name="action" value="eventflow_save_display">
                    <?php wp_nonce_field('eventflow_save_display'); ?>
                    <table class="form-table" role="presentation">
                        <tr>
                            <th scope="row"><label for="eventflow_primary_colour"><?php esc_html_e('Primary colour', 'eventflow-woocommerce'); ?></label></th>
                            <td><input type="color" id="eventflow_primary_colour" name="primary_colour" value="<?php echo esc_attr($display['primary_colour'] ?? '#1f4d3a'); ?>"></td>
                        </tr>
                        <tr>
                            <th scope="row"><label for="eventflow_accent_colour"><?php esc_html_e('Accent colour', 'eventflow-woocommerce'); ?></label></th>
                            <td><input type="color" id="eventflow_accent_colour" name="accent_colour" value="<?php echo esc_attr($display['accent_colour'] ?? '#d4a72c'); ?>"></td>
                        </tr>
                    </table>
                    <?php submit_button(__('Save email appearance')); ?>
                </form>
            </div>

            <div class="card" style="max-width:760px;padding:20px;margin-top:20px;">
                <h2><?php esc_html_e('Getting started', 'eventflow-woocommerce'); ?></h2>
                <ol>
                    <li><?php esc_html_e('Register this website using a one-time registration code.', 'eventflow-woocommerce'); ?></li>
                    <li><?php esc_html_e('Edit a WooCommerce product and enable “This is a ticket product”.', 'eventflow-woocommerce'); ?></li>
                    <li><?php esc_html_e('Enter the Event ID and ticket type, and make sure the product has a SKU. The SKU becomes the ticket prefix.', 'eventflow-woocommerce'); ?></li>
                    <li><?php esc_html_e('When a paid order reaches Processing or Completed, EventFlow generates the tickets and emails them to the customer.', 'eventflow-woocommerce'); ?></li>
                </ol>
            </div>
        </div>
        <?php
    }

    public function render_reminders_page(): void
    {
        if (!current_user_can('manage_woocommerce')) {
            return;
        }

        $orders = wc_get_orders([
            'limit' => 50,
            'status' => ['processing', 'completed'],
            'orderby' => 'date',
            'order' => 'DESC',
            'meta_query' => [
                [
                    'key' => '_eventflow_tickets',
                    'compare' => 'EXISTS',
                ],
            ],
        ]);
        ?>
        <div class="wrap">
            <h1><?php esc_html_e('EventFlow ticket reminders', 'eventflow-woocommerce'); ?></h1>
            <?php $this->render_notice(); ?>
            <p><?php esc_html_e('Send a reminder email containing the tickets already stored against an order. This does not generate new tickets.', 'eventflow-woocommerce'); ?></p>
            <table class="widefat striped">
                <thead><tr>
                    <th><?php esc_html_e('Order', 'eventflow-woocommerce'); ?></th>
                    <th><?php esc_html_e('Customer', 'eventflow-woocommerce'); ?></th>
                    <th><?php esc_html_e('Email', 'eventflow-woocommerce'); ?></th>
                    <th><?php esc_html_e('Tickets', 'eventflow-woocommerce'); ?></th>
                    <th><?php esc_html_e('Action', 'eventflow-woocommerce'); ?></th>
                </tr></thead>
                <tbody>
                <?php if (!$orders) : ?>
                    <tr><td colspan="5"><?php esc_html_e('No orders with EventFlow tickets were found.', 'eventflow-woocommerce'); ?></td></tr>
                <?php else : foreach ($orders as $order) :
                    $tickets = $order->get_meta('_eventflow_tickets', true);
                    $tickets = is_array($tickets) ? $tickets : [];
                    if (!$tickets) {
                        continue;
                    }
                    ?>
                    <tr>
                        <td>#<?php echo esc_html($order->get_order_number()); ?></td>
                        <td><?php echo esc_html($order->get_formatted_billing_full_name()); ?></td>
                        <td><?php echo esc_html($order->get_billing_email()); ?></td>
                        <td><?php echo esc_html((string) count($tickets)); ?></td>
                        <td>
                            <form method="post" action="<?php echo esc_url(admin_url('admin-post.php')); ?>">
                                <input type="hidden" name="action" value="eventflow_send_reminder">
                                <input type="hidden" name="order_id" value="<?php echo esc_attr((string) $order->get_id()); ?>">
                                <?php wp_nonce_field('eventflow_send_reminder_' . $order->get_id()); ?>
                                <?php submit_button(__('Send reminder', 'eventflow-woocommerce'), 'secondary small', 'submit', false); ?>
                            </form>
                        </td>
                    </tr>
                <?php endforeach; endif; ?>
                </tbody>
            </table>
        </div>
        <?php
    }

    public function register_site(): void
    {
        if (!current_user_can('manage_woocommerce')) {
            wp_die(esc_html__('You are not allowed to manage EventFlow.', 'eventflow-woocommerce'));
        }
        check_admin_referer('eventflow_register_site');

        $api_url = isset($_POST['api_url']) ? esc_url_raw(wp_unslash($_POST['api_url'])) : '';
        $code = isset($_POST['registration_code']) ? sanitize_text_field(wp_unslash($_POST['registration_code'])) : '';
        $result = EventFlow_WC_API::register_site($api_url, $code);

        if (is_wp_error($result)) {
            $this->redirect_with_notice('error', $result->get_error_message());
        }
        $this->redirect_with_notice('success', $result['message'] ?? __('Website registered successfully.', 'eventflow-woocommerce'));
    }

    public function disconnect_site(): void
    {
        if (!current_user_can('manage_woocommerce')) {
            wp_die(esc_html__('You are not allowed to manage EventFlow.', 'eventflow-woocommerce'));
        }
        check_admin_referer('eventflow_disconnect_site');
        EventFlow_WC_API::disconnect();
        $this->redirect_with_notice('success', __('This website has been disconnected from EventFlow.', 'eventflow-woocommerce'));
    }

    public function save_display(): void
    {
        if (!current_user_can('manage_woocommerce')) {
            wp_die(esc_html__('You are not allowed to manage EventFlow.', 'eventflow-woocommerce'));
        }
        check_admin_referer('eventflow_save_display');

        $primary = isset($_POST['primary_colour']) ? sanitize_hex_color(wp_unslash($_POST['primary_colour'])) : '#1f4d3a';
        $accent = isset($_POST['accent_colour']) ? sanitize_hex_color(wp_unslash($_POST['accent_colour'])) : '#d4a72c';
        update_option('eventflow_wc_display', [
            'primary_colour' => $primary ?: '#1f4d3a',
            'accent_colour' => $accent ?: '#d4a72c',
        ], false);

        $this->redirect_with_notice('success', __('Ticket email appearance saved.', 'eventflow-woocommerce'));
    }

    public function send_reminder(): void
    {
        if (!current_user_can('manage_woocommerce')) {
            wp_die(esc_html__('You are not allowed to manage EventFlow.', 'eventflow-woocommerce'));
        }

        $order_id = isset($_POST['order_id']) ? absint($_POST['order_id']) : 0;
        check_admin_referer('eventflow_send_reminder_' . $order_id);
        $order = wc_get_order($order_id);
        if (!$order instanceof WC_Order) {
            $this->redirect_with_notice('error', __('The order could not be found.', 'eventflow-woocommerce'), 'eventflow-reminders');
        }

        $tickets = $order->get_meta('_eventflow_tickets', true);
        if (!is_array($tickets) || !$tickets) {
            $this->redirect_with_notice('error', __('No stored EventFlow tickets were found for this order.', 'eventflow-woocommerce'), 'eventflow-reminders');
        }

        $sent = EventFlow_WC_Email::send($order, $tickets, true);
        $this->redirect_with_notice(
            $sent ? 'success' : 'error',
            $sent ? __('Ticket reminder sent.', 'eventflow-woocommerce') : __('The reminder email could not be sent. Check your WordPress email configuration.', 'eventflow-woocommerce'),
            'eventflow-reminders'
        );
    }

    private function render_notice(): void
    {
        if (empty($_GET['eventflow_notice'])) {
            return;
        }

        $type = sanitize_key(wp_unslash($_GET['eventflow_notice']));
        $message = isset($_GET['eventflow_message']) ? sanitize_text_field(wp_unslash($_GET['eventflow_message'])) : '';
        if (!in_array($type, ['success', 'error'], true) || $message === '') {
            return;
        }

        printf('<div class="notice notice-%1$s is-dismissible"><p>%2$s</p></div>', esc_attr($type), esc_html($message));
    }

    private function redirect_with_notice(string $type, string $message, string $page = 'eventflow'): void
    {
        $url = add_query_arg([
            'page' => $page,
            'eventflow_notice' => $type,
            'eventflow_message' => rawurlencode($message),
        ], admin_url('admin.php'));
        wp_safe_redirect($url);
        exit;
    }
}
