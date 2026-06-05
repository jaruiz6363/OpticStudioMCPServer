# Project Rules

## Environment
- Windows-only application
- User does NOT have Python installed
- **NEVER use Python** for any scripting, plotting, or tooling in this project
- For graphing/plotting, use Microsoft tools (Excel/PowerShell) or HTML+JavaScript
- For scripting, use PowerShell or batch files
- If MCP server produces files, they should be saved in `.tmp` directory

## Zemax Connection
- **Always connect in `standalone` mode by default** when using `zemax_connect`
- Only use `extension` mode if the user explicitly asks for it
