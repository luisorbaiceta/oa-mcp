const { McpServer } = require("@modelcontextprotocol/sdk/server/mcp.js");
const { StdioServerTransport } = require("@modelcontextprotocol/sdk/server/stdio.js");
const fs = require('fs');
const path = require('path');
const net = require('net');
const { z } = require('zod');

// Configuration
const REVIT_PORT = 5000;
const MANIFEST_PATH = path.join(
    process.env.ProgramData || 'C:\\ProgramData',
    'RevitMcp',
    'manifest.json'
);

// Initialize MCP Server
const server = new McpServer({
    name: "RevitMcpServer",
    version: "1.0.0"
});

// Helper: Send command to Revit via TCP
function sendToRevit(commandName, args) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();

        // 30s Timeout to prevent indefinite hanging
        const timeout = setTimeout(() => {
            client.destroy();
            resolve({
                content: [{
                    type: "text",
                    text: "Error: Request to Revit timed out after 30 seconds. Is the plugin responsive?"
                }],
                isError: true
            });
        }, 30000);

        client.connect(REVIT_PORT, '127.0.0.1', () => {
            const request = JSON.stringify({ command: commandName, args: args });
            client.write(request);
            client.end(); // Signal end of request to allow server to read completely
        });

        client.on('data', (data) => {
            clearTimeout(timeout);
            try {
                let responseStr = data.toString();
                // Strip BOM if present
                if (responseStr.charCodeAt(0) === 0xFEFF) {
                    responseStr = responseStr.slice(1);
                }
                const response = JSON.parse(responseStr);
                client.destroy(); // Kill client after response

                if (response.Success) {
                    resolve({
                        content: [{
                            type: "text",
                            text: JSON.stringify(response.Data, null, 2)
                        }]
                    });
                } else {
                    resolve({
                        content: [{
                            type: "text",
                            text: `Error from Revit: ${response.Message}`
                        }],
                        isError: true
                    });
                }
            } catch (err) {
                client.destroy();
                resolve({
                    content: [{
                        type: "text",
                        text: `Error parsing response: ${err.message}`
                    }],
                    isError: true
                });
            }
        });

        client.on('error', (err) => {
            clearTimeout(timeout);
            client.destroy();
            resolve({
                content: [{
                    type: "text",
                    text: `Connection Error: ${err.message}. Is Revit running with the plugin?`
                }],
                isError: true
            });
        });
    });
}

// Dynamic Tool Registration
function loadTools() {
    if (!fs.existsSync(MANIFEST_PATH)) {
        console.error(`Manifest not found at ${MANIFEST_PATH}`);
        return;
    }

    try {
        const data = fs.readFileSync(MANIFEST_PATH, 'utf8');
        const manifest = JSON.parse(data);

        // Clear existing tools if possible (SDK might not expose unregister, 
        // so we might need to restart server logic or just add unique ones. 
        // For this generic server, we assume start-up load. 
        // Hot-reload might require restarting the process or SDK support.)

        manifest.tools.forEach(tool => {
            // Convert JSON Schema to Zod if possible, or use a generic schema.
            // The SDK expects Zod schemas for validation. 
            // We can construct a dynamic Zod schema or use a permissive one and let Revit validate.

            // For simplicity and robustness, we use a permissive schema here 
            // because strict type mapping from JSON schema to Zod dynamically is complex.
            // We blindly pass args to Revit.

            const shape = {};
            if (tool.inputSchema && tool.inputSchema.properties) {
                for (const [key, prop] of Object.entries(tool.inputSchema.properties)) {
                    // Simple mapping
                    // We assume checking is done by Revit or we do basic checks here.
                    // To make it work with SDK zod implementation:
                    shape[key] = z.any().describe(prop.description || "");
                }
            }

            server.tool(
                tool.name,
                tool.description,
                shape,
                async (args) => {
                    return await sendToRevit(tool.name, args);
                }
            );
        });

        console.error(`Loaded ${manifest.tools.length} tools from manifest.`);
    } catch (err) {
        console.error("Error loading manifest:", err);
    }
}

// Initial Load
loadTools();

// Hot Reload Watcher
fs.watchFile(MANIFEST_PATH, (curr, prev) => {
    console.error("Manifest changed, reloading tools...");
    // Note: SDK doesn't support unregistering tools easily in current version.
    // For a robust system, we might need to exit to let host restart us, 
    // or just assume new tools are added. 
    // Ideally, we'd restart the process.
    // process.exit(0); // Forcing restart by the client (Claude/MCP Client) is often the safest bet for schema updates.
});

// Start the server transport
const transport = new StdioServerTransport();
server.connect(transport);
