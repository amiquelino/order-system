import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "OrderSystem",
  description: "Order System frontend",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body suppressHydrationWarning>
        <nav style={{ padding: "1rem 2rem", borderBottom: "1px solid #ccc" }}>
          <Link href="/" style={{ marginRight: "1rem" }}>
            Create Order
          </Link>
        </nav>
        {children}
      </body>
    </html>
  );
}
