<?php
/**
 * Plugin Name: EventFlow for WooCommerce
 * Description: Connect WooCommerce ticket products to the central EventFlow ticketing platform.
 * Version: 1.0.0
 * Requires at least: 6.2
 * Requires PHP: 8.0
 * Author: EventFlow
 * Text Domain: eventflow-woocommerce
 */

if (!defined('ABSPATH')) {
    exit;
}

define('EVENTFLOW_WC_VERSION', '1.0.0');
define('EVENTFLOW_WC_FILE', __FILE__);
define('EVENTFLOW_WC_PATH', plugin_dir_path(__FILE__));
define('EVENTFLOW_WC_URL', plugin_dir_url(__FILE__));

require_once EVENTFLOW_WC_PATH . 'includes/class-eventflow-api.php';
require_once EVENTFLOW_WC_PATH . 'includes/class-eventflow-email.php';
require_once EVENTFLOW_WC_PATH . 'includes/class-eventflow-woocommerce.php';
require_once EVENTFLOW_WC_PATH . 'includes/class-eventflow-admin.php';

add_action('plugins_loaded', static function () {
    if (!class_exists('WooCommerce')) {
        add_action('admin_notices', static function () {
            if (current_user_can('activate_plugins')) {
                echo '<div class="notice notice-warning"><p>';
                echo esc_html__('EventFlow for WooCommerce requires WooCommerce to be installed and active.', 'eventflow-woocommerce');
                echo '</p></div>';
            }
        });
        return;
    }

    EventFlow_WC_Admin::instance();
    EventFlow_WC_WooCommerce::instance();
});
