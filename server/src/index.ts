import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { readConfig, RevitBridge } from "./bridge.js";
import { createServer } from "./tools.js";
import { loadPrivateTools } from "./private-loader.js";

async function main() {
  const path = process.env.TYBIM_MCP_CONFIG;
  if (!path) throw new Error("Set TYBIM_MCP_CONFIG to the absolute bridge.config.json path");
  const bridge = new RevitBridge(readConfig(path));
  const server = createServer(bridge);
  await loadPrivateTools(server, bridge);
  await server.connect(new StdioServerTransport());
}
main().catch(error => { console.error(error instanceof Error ? error.message : String(error)); process.exitCode = 1; });
