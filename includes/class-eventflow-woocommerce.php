<?php
if (!defined('ABSPATH')) {
    exit;
}

final class EventFlow_WC_WooCommerce
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
        add_action('woocommerce_product_options_general_product_data', [$this, 'product_fields']);
        add_action('woocommerce_admin_process_product_object', [$this, 'save_product_fields']);
        add_action('woocommerce_variation_options_pricing', [$this, 'variation_fields'], 10, 3);
        add_action('woocommerce_save_product_variation', [$this, 'save_variation_fields'], 10, 2);

        add_action('woocommerce_order_status_processing', [$this, 'generate_for_order'], 20);
        add_action('woocommerce_order_status_completed', [$this, 'generate_for_order'], 20);

        add_filter('woocommerce_order_actions', [$this, 'add_resend_action']);
        add_action('woocommerce_order_action_eventflow_resend_tickets', [$this, 'resend_order_tickets']);
    }

    public function product_fields(): void
    {
        echo '<div class="options_group">';
        echo '<h4 style="padding:10px 12px 0;">' . esc_html__('EventFlow ticket settings', 'eventflow-woocommerce') . '</h4>';

        woocommerce_wp_checkbox([
            'id' => '_eventflow_ticket',
            'label' => __('This is a ticket product', 'eventflow-woocommerce'),
            'description' => __('Generate EventFlow tickets when this product is purchased.', 'eventflow-woocommerce'),
            'desc_tip' => true,
        ]);

        woocommerce_wp_text_input([
            'id' => '_eventflow_event_id',
            'label' => __('Event ID', 'eventflow-woocommerce'),
            'description' => __('A stable ID for this event, shared by all ticket products for the same event. The event is created automatically on the first purchase.', 'eventflow-woocommerce'),
            'desc_tip' => true,
        ]);

        woocommerce_wp_text_input([
            'id' => '_eventflow_ticket_type',
            'label' => __('Ticket type', 'eventflow-woocommerce'),
            'placeholder' => 'e.g. Adult, Child, VIP',
        ]);

        woocommerce_wp_text_input([
            'id' => '_eventflow_event_date',
            'label' => __('Event date', 'eventflow-woocommerce'),
            'type' => 'text',
            'placeholder' => 'e.g. 2027-06-19',
            'description' => __('Optional event date sent to EventFlow.', 'eventflow-woocommerce'),
        ]);

        echo '<p class="form-field"><label>' . esc_html__('Ticket prefix', 'eventflow-woocommerce') . '</label><span class="description">' . esc_html__('The product SKU is used as the ticket prefix.', 'eventflow-woocommerce') . '</span></p>';
        echo '</div>';
    }

    public function save_product_fields(WC_Product $product): void
    {
        $product->update_meta_data('_eventflow_ticket', isset($_POST['_eventflow_ticket']) ? 'yes' : 'no');

        foreach (['_eventflow_event_id', '_eventflow_ticket_type', '_eventflow_event_date'] as $key) {
            if (isset($_POST[$key])) {
                $product->update_meta_data($key, sanitize_text_field(wp_unslash($_POST[$key])));
            }
        }
    }

    public function variation_fields(int $loop, array $variation_data, WP_Post $variation): void
    {
        $id = $variation->ID;
        ?>
        <div class="form-row form-row-full">
            <label>
                <input type="checkbox" name="eventflow_ticket[<?php echo esc_attr($id); ?>]" value="yes" <?php checked(get_post_meta($id, '_eventflow_ticket', true), 'yes'); ?>>
                <?php esc_html_e('This variation is an EventFlow ticket', 'eventflow-woocommerce'); ?>
            </label>
        </div>
        <p class="form-row form-row-first">
            <label><?php esc_html_e('Event ID', 'eventflow-woocommerce'); ?></label>
            <input type="text" name="eventflow_event_id[<?php echo esc_attr($id); ?>]" value="<?php echo esc_attr(get_post_meta($id, '_eventflow_event_id', true)); ?>">
        </p>
        <p class="form-row form-row-last">
            <label><?php esc_html_e('Ticket type', 'eventflow-woocommerce'); ?></label>
            <input type="text" name="eventflow_ticket_type[<?php echo esc_attr($id); ?>]" value="<?php echo esc_attr(get_post_meta($id, '_eventflow_ticket_type', true)); ?>">
        </p>
        <p class="form-row form-row-full">
            <label><?php esc_html_e('Event date', 'eventflow-woocommerce'); ?></label>
            <input type="text" name="eventflow_event_date[<?php echo esc_attr($id); ?>]" value="<?php echo esc_attr(get_post_meta($id, '_eventflow_event_date', true)); ?>" placeholder="2027-06-19">
        </p>
        <?php
    }

    public function save_variation_fields(int $variation_id, int $i): void
    {
        update_post_meta($variation_id, '_eventflow_ticket', isset($_POST['eventflow_ticket'][$variation_id]) ? 'yes' : 'no');

        foreach (['event_id', 'ticket_type', 'event_date'] as $field) {
            $key = 'eventflow_' . $field;
            if (isset($_POST[$key][$variation_id])) {
                update_post_meta($variation_id, '_eventflow_' . $field, sanitize_text_field(wp_unslash($_POST[$key][$variation_id])));
            }
        }
    }

    private function product_meta(WC_Product $product, string $key): string
    {
        $value = (string) get_post_meta($product->get_id(), $key, true);
        if ($value === '' && $product->get_parent_id()) {
            $value = (string) get_post_meta($product->get_parent_id(), $key, true);
        }
        return $value;
    }

    public function generate_for_order(int $order_id): void
    {
        $order = wc_get_order($order_id);
        if (!$order instanceof WC_Order || !EventFlow_WC_API::is_connected()) {
            return;
        }

        $all_tickets = $order->get_meta('_eventflow_tickets', true);
        $all_tickets = is_array($all_tickets) ? $all_tickets : [];
        $new_tickets = [];

        foreach ($order->get_items('line_item') as $item_id => $item) {
            $product = $item->get_product();
            if (!$product instanceof WC_Product) {
                continue;
            }

            if ($this->product_meta($product, '_eventflow_ticket') !== 'yes') {
                continue;
            }

            $event_id = $this->product_meta($product, '_eventflow_event_id');
            $sku = $product->get_sku();
            $quantity = (int) $item->get_quantity();

            if ($event_id === '' || $sku === '' || $quantity < 1) {
                $order->add_order_note(sprintf(
                    'EventFlow: ticket line %d was skipped because Event ID, product SKU or quantity is missing.',
                    (int) $item_id
                ));
                continue;
            }

            $stored_line_tickets = $item->get_meta('_eventflow_tickets', true);
            if (is_array($stored_line_tickets) && count($stored_line_tickets) >= $quantity) {
                continue;
            }

            $ticket_type = $this->product_meta($product, '_eventflow_ticket_type');
            if ($ticket_type === '') {
                $ticket_type = __('Ticket', 'eventflow-woocommerce');
            }

            $event_date = $this->product_meta($product, '_eventflow_event_date');

            $payload = [
                'eventId' => $event_id,
                'eventName' => $product->get_name(),
                'eventDate' => $event_date,
                'ticketPrefix' => strtoupper(trim($sku)),
                'orderId' => (string) $order->get_id(),
                'orderItemId' => (string) $item_id,
                'quantity' => $quantity,
                'ticketType' => $ticket_type,
                'customerName' => $order->get_formatted_billing_full_name(),
                'customerEmail' => $order->get_billing_email(),
            ];

            $result = EventFlow_WC_API::request('POST', '/tickets/generate', $payload);
            if (is_wp_error($result)) {
                $order->add_order_note('EventFlow ticket generation failed for line item ' . (int) $item_id . ': ' . $result->get_error_message());
                error_log('EventFlow: order ' . $order_id . ', item ' . $item_id . ': ' . $result->get_error_message());
                continue;
            }

            $line_tickets = [];
            foreach ($result as $ticket) {
                if (!is_array($ticket) || empty($ticket['ticketNumber'])) {
                    continue;
                }
                $record = [
                    'ticket_number' => sanitize_text_field($ticket['ticketNumber']),
                    'ticket_type' => $ticket_type,
                    'event_name' => $product->get_name(),
                    'event_id' => $event_id,
                    'order_item_id' => (string) $item_id,
                ];
                $line_tickets[] = $record;
                $new_tickets[] = $record;
            }

            if ($line_tickets) {
                $item->update_meta_data('_eventflow_tickets', $line_tickets);
                $item->save();

                $all_tickets = array_values(array_filter($all_tickets, static function ($ticket) use ($item_id) {
                    return (string) ($ticket['order_item_id'] ?? '') !== (string) $item_id;
                }));
                $all_tickets = array_merge($all_tickets, $line_tickets);
                $order->update_meta_data('_eventflow_tickets', $all_tickets);
                $order->save();
            }
        }

        if ($new_tickets) {
            $sent = EventFlow_WC_Email::send($order, $all_tickets);
            if ($sent) {
                $order->update_meta_data('_eventflow_ticket_email_sent', current_time('mysql'));
                $order->save();
                $order->add_order_note(sprintf('EventFlow: %d ticket(s) generated and ticket email sent.', count($new_tickets)));
            } else {
                $order->add_order_note('EventFlow: tickets were generated, but the ticket email could not be sent. Use the resend order action.');
            }
        }
    }

    public function add_resend_action(array $actions): array
    {
        $actions['eventflow_resend_tickets'] = __('Resend EventFlow tickets', 'eventflow-woocommerce');
        return $actions;
    }

    public function resend_order_tickets(WC_Order $order): void
    {
        $tickets = $order->get_meta('_eventflow_tickets', true);
        if (!is_array($tickets) || !$tickets) {
            $order->add_order_note('EventFlow resend failed: no stored tickets were found.');
            return;
        }

        if (EventFlow_WC_Email::send($order, $tickets)) {
            $order->add_order_note('EventFlow tickets resent. Existing tickets were reused; no new tickets were generated.');
        } else {
            $order->add_order_note('EventFlow ticket resend failed. Check the site mail configuration.');
        }
    }
}
