import { lazy, type ComponentType } from 'react';
import { Route, Routes } from 'react-router';
import { AdminLayout } from './AdminLayout';

/** Lazy-loads one named page component from a module (each admin area is its own chunk). */
const pick = <M, K extends keyof M>(loader: () => Promise<M>, name: K) =>
  lazy(() => loader().then((m) => ({ default: m[name] as unknown as ComponentType })));

const dashboard = () => import('./pages/DashboardPages');
const products = () => import('./pages/ProductPages');
const catalog = () => import('./pages/CatalogPages');
const orders = () => import('./pages/OrderPages');
const site = () => import('./pages/CustomizationPages');
const homepage = () => import('./pages/HomepageBuilder');
const content = () => import('./pages/ContentPagesAdmin');
const engagement = () => import('./pages/EngagementPages');
const settings = () => import('./pages/SettingsPages');

const Dashboard = pick(dashboard, 'DashboardPage');
const Reports = pick(dashboard, 'ReportsPage');
const Products = pick(products, 'ProductListPage');
const ProductEditor = pick(products, 'ProductEditorPage');
const Inventory = pick(products, 'InventoryPage');
const Categories = pick(catalog, 'CategoriesPage');
const Collections = pick(catalog, 'CollectionsPage');
const Media = pick(catalog, 'MediaLibraryPage');
const Orders = pick(orders, 'OrderListPage');
const OrderDetail = pick(orders, 'OrderDetailPage');
const Customers = pick(orders, 'CustomersPage');
const CustomerDetail = pick(orders, 'CustomerDetailPage');
const Customize = pick(site, 'CustomizationPage');
const Homepage = pick(homepage, 'HomepageBuilderPage');
const Pages = pick(content, 'ContentPagesListPage');
const PageEditor = pick(content, 'ContentPageEditorPage');
const Promotions = pick(engagement, 'PromotionsPage');
const Reviews = pick(engagement, 'ReviewsPage');
const Enquiries = pick(engagement, 'EnquiriesPage');
const Settings = pick(settings, 'StoreSettingsPage');
const Audit = pick(settings, 'AuditLogPage');
const Profile = pick(settings, 'AdminProfilePage');
const NotFound = pick(settings, 'AdminNotFound');

export default function AdminApp() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route index element={<Dashboard />} />
        <Route path="reports" element={<Reports />} />
        <Route path="products" element={<Products />} />
        <Route path="products/new" element={<ProductEditor />} />
        <Route path="products/:id" element={<ProductEditor />} />
        <Route path="inventory" element={<Inventory />} />
        <Route path="categories" element={<Categories />} />
        <Route path="collections" element={<Collections />} />
        <Route path="media" element={<Media />} />
        <Route path="orders" element={<Orders />} />
        <Route path="orders/:id" element={<OrderDetail />} />
        <Route path="customers" element={<Customers />} />
        <Route path="customers/:id" element={<CustomerDetail />} />
        <Route path="customize" element={<Customize />} />
        <Route path="homepage" element={<Homepage />} />
        <Route path="pages" element={<Pages />} />
        <Route path="pages/:id" element={<PageEditor />} />
        <Route path="promotions" element={<Promotions />} />
        <Route path="reviews" element={<Reviews />} />
        <Route path="enquiries" element={<Enquiries />} />
        <Route path="settings" element={<Settings />} />
        <Route path="audit" element={<Audit />} />
        <Route path="profile" element={<Profile />} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}
