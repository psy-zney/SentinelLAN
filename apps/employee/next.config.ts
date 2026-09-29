import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  basePath: "/employee",
  env: { NEXT_PUBLIC_PORTAL: "employee", NEXT_PUBLIC_BASE_PATH: "/employee", NEXT_PUBLIC_COMPANY_URL: process.env.NEXT_PUBLIC_COMPANY_URL ?? (process.env.NODE_ENV === "development" ? "http://localhost:3000/company" : "/company"), NEXT_PUBLIC_EMPLOYEE_URL: process.env.NEXT_PUBLIC_EMPLOYEE_URL ?? (process.env.NODE_ENV === "development" ? "http://localhost:3001/employee" : "/employee") },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Referrer-Policy", value: "no-referrer" },
          { key: "Permissions-Policy", value: "camera=(self), microphone=(), geolocation=()" },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" }
        ]
      }
    ];
  },
};

export default nextConfig;
