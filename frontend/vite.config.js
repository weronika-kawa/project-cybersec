import { defineConfig } from "vite";
export default defineConfig({ server: { proxy: { "/api": "http://localhost:5000", "/swagger": "http://localhost:5000" } } });
