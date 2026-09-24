import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
  globalIgnores([".expo/**", "node_modules/**", "dist/**", "coverage/**", "*.d.ts"]),
  {
    rules: {
      "no-unused-vars": "off",
    },
  },
]);
