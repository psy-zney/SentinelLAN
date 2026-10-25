type ContentSecurityPolicyOptions = {
  nonce: string;
  development: boolean;
  apiUrl: string;
  pageOrigin: string;
};

function networkOrigin(value: string) {
  const url = new URL(value);
  if (!["http:", "https:"].includes(url.protocol) || url.username || url.password) {
    throw new Error("The portal requires an HTTP(S) API origin without credentials.");
  }
  return url;
}

export function contentSecurityPolicy({ nonce, development, apiUrl, pageOrigin }: ContentSecurityPolicyOptions) {
  if (!/^[A-Za-z0-9+/]+={0,2}$/.test(nonce)) throw new Error("Invalid CSP nonce.");
  const connect = new Set(["'self'"]);
  const images = new Set(["'self'", "data:", "blob:"]);
  const origins = [networkOrigin(pageOrigin)];
  if (apiUrl && !apiUrl.startsWith("/")) {
    const api = networkOrigin(apiUrl);
    origins.push(api);
    images.add(api.origin);
  }
  for (const url of origins) {
    connect.add(url.origin);
    url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
    connect.add(url.origin);
  }
  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${development ? " 'unsafe-eval'" : ""}`,
    // React style attributes and Motion currently require inline styles, never inline scripts.
    "style-src 'self' 'unsafe-inline'",
    `connect-src ${[...connect].join(" ")}`,
    `img-src ${[...images].join(" ")}`,
    "font-src 'self'",
    "worker-src 'self' blob:",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'"
  ].join("; ");
}
