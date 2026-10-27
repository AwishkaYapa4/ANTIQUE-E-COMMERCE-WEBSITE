import { lazy } from 'react';
import { createBrowserRouter } from 'react-router';
import { CustomerLayout } from '@/layouts/customer/CustomerLayout';
import { RequireAdmin, RequireAuth } from '@/features/auth/Guards';

// Route-level code splitting: each page is loaded on demand.
const HomePage = lazy(() => import('@/features/customer/home/HomePage'));
const ProductPage = lazy(() => import('@/features/customer/product/ProductPage'));
const CheckoutPage = lazy(() => import('@/features/customer/checkout/CheckoutPage'));
const shop = () => import('@/features/customer/shop/ShopPages');
const ShopPage = lazy(() => shop().then((m) => ({ default: m.ShopPage })));
const CategoryPage = lazy(() => shop().then((m) => ({ default: m.CategoryPage })));
const CollectionPage = lazy(() => shop().then((m) => ({ default: m.CollectionPage })));
const CollectionsPage = lazy(() => shop().then((m) => ({ default: m.CollectionsPage })));
const pages = () => import('@/features/customer/pages/ContentPages');
const ContentPage = lazy(() => pages().then((m) => ({ default: m.ContentPage })));
const ContactPage = lazy(() => pages().then((m) => ({ default: m.ContactPage })));
const CartPage = lazy(() => pages().then((m) => ({ default: m.CartPage })));
const WishlistPage = lazy(() => pages().then((m) => ({ default: m.WishlistPage })));
const NotFoundPage = lazy(() => pages().then((m) => ({ default: m.NotFoundPage })));
const auth = () => import('@/features/auth/AuthPages');
const LoginPage = lazy(() => auth().then((m) => ({ default: m.LoginPage })));
const RegisterPage = lazy(() => auth().then((m) => ({ default: m.RegisterPage })));
const ForgotPasswordPage = lazy(() => auth().then((m) => ({ default: m.ForgotPasswordPage })));
const VerifyEmailPage = lazy(() => auth().then((m) => ({ default: m.VerifyEmailPage })));
const orders = () => import('@/features/customer/orders/OrderViews');
const OrderConfirmationPage = lazy(() => orders().then((m) => ({ default: m.OrderConfirmationPage })));
const account = () => import('@/features/customer/account/AccountPages');
const AccountLayout = lazy(() => account().then((m) => ({ default: m.AccountLayout })));
const AccountOverview = lazy(() => account().then((m) => ({ default: m.AccountOverview })));
const AccountOrders = lazy(() => account().then((m) => ({ default: m.AccountOrders })));
const AccountOrderDetail = lazy(() => account().then((m) => ({ default: m.AccountOrderDetail })));
const AccountAddresses = lazy(() => account().then((m) => ({ default: m.AccountAddresses })));
const AccountSettings = lazy(() => account().then((m) => ({ default: m.AccountSettings })));
const AdminApp = lazy(() => import('@/features/admin/AdminApp'));

export const router = createBrowserRouter([
  {
    element: <CustomerLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'shop', element: <ShopPage /> },
      { path: 'category/:slug', element: <CategoryPage /> },
      { path: 'collections', element: <CollectionsPage /> },
      { path: 'collections/:slug', element: <CollectionPage /> },
      { path: 'products/:slug', element: <ProductPage /> },
      { path: 'pages/:slug', element: <ContentPage /> },
      { path: 'contact', element: <ContactPage /> },
      { path: 'cart', element: <CartPage /> },
      { path: 'wishlist', element: <WishlistPage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'forgot-password', element: <ForgotPasswordPage /> },
      { path: 'verify-email', element: <VerifyEmailPage /> },
      { path: 'checkout', element: <RequireAuth><CheckoutPage /></RequireAuth> },
      { path: 'order-confirmation/:id', element: <RequireAuth><OrderConfirmationPage /></RequireAuth> },
      {
        path: 'account',
        element: <RequireAuth><AccountLayout /></RequireAuth>,
        children: [
          { index: true, element: <AccountOverview /> },
          { path: 'orders', element: <AccountOrders /> },
          { path: 'orders/:id', element: <AccountOrderDetail /> },
          { path: 'addresses', element: <AccountAddresses /> },
          { path: 'settings', element: <AccountSettings /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  { path: 'admin/*', element: <RequireAdmin><AdminApp /></RequireAdmin> },
]);


