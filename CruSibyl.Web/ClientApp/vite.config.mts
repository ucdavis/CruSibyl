import { UserConfig, defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import fs from "fs";
import path from "path";
import { spawn } from "child_process";

// Get base folder for certificates.
const baseFolder =
  process.env.APPDATA !== undefined && process.env.APPDATA !== ""
    ? `${process.env.APPDATA}/ASP.NET/https`
    : `${process.env.HOME}/.aspnet/https`;

// Generate the certificate name using the NPM package name
const certificateName = process.env.npm_package_name;

// Define certificate filepath
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
// Define key filepath
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

export default defineConfig(async ({ mode }) => {
  // Ensure the certificate and key exist
  if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    // Wait for the certificate to be generated
    await new Promise<void>((resolve) => {
      spawn(
        "dotnet",
        [
          "dev-certs",
          "https",
          "--export-path",
          certFilePath,
          "--format",
          "Pem",
          "--no-password",
        ],
        { stdio: "inherit" }
      ).on("exit", (code) => {
        resolve();
        if (code) {
          process.exit(code);
        }
      });
    });
  }

  // Load app-level env vars to node-level env vars.
  const env = { ...process.env, ...loadEnv(mode, process.cwd()) };
  process.env = env;

  const target = env.ASPNETCORE_HTTPS_PORT
    ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
    : env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(";")[0]
    : "http://localhost:7182";

  const config: UserConfig = {
    // appType: "custom",
    root: "src",
    publicDir: "public",
    //base: "./",
    build: {
      outDir: "build",
    },
    plugins: [react()],
    optimizeDeps: {
      include: [],
    },
    server: {
      port: 5173,
      hmr: {
        clientPort: 5173,
      },
      strictPort: true,
      // Uncomment the following to enable reverse proxy support
      // https: {
      //   cert: certFilePath,
      //   key: keyFilePath,
      // },
      // proxy: {
      //   "/api": {
      //     target: target,
      //     // Handle errors to prevent the proxy middleware from crashing when
      //     // the ASP NET Core webserver is unavailable
      //     onError: (err, req, resp, target) => {
      //       console.error(`${err.message}`);
      //     },
      //     changeOrigin: true,
      //     secure: false,
      //     rewrite: (path) => path.replace(/^\/api/, "/api"),
      //     // Uncomment this line to add support for proxying websockets
      //     //ws: true,
      //     headers: {
      //       Connection: "Keep-Alive",
      //     },
      //   },
      //   "/swagger": {
      //     target: target,
      //     // Handle errors to prevent the proxy middleware from crashing when
      //     // the ASP NET Core webserver is unavailable
      //     onError: (err, req, resp, target) => {
      //       console.error(`${err.message}`);
      //     },
      //     changeOrigin: true,
      //     secure: false,
      //     rewrite: (path) => path.replace(/^\/swagger/, "/swagger"),
      //     // Uncomment this line to add support for proxying websockets
      //     //ws: true,
      //     headers: {
      //       Connection: "Keep-Alive",
      //     },
      //   },
      // },
    },
  };

  return config;
});
