---
name: animator-get-data
description: Inspect a Unity `AnimatorController` asset — controller name, every parameter (name, type, defaults), every layer with its state machine, every state, and every transition. Pair with 'animator-modify' to write changes back.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Animator / Get Data

Inspect a Unity `AnimatorController` asset. Returns the controller's name plus the full set of parameters, layers, states, and transitions — enough to drive a follow-up 'animator-modify' call with valid names.

## Inputs

- `animatorRef` — reference to the `AnimatorController` asset (path must start with `Assets/` and end with `.controller`).

## Returned fields

- `name` — controller asset name.
- `parameters` — every parameter (name, type, default value).
- `layers` — every layer with its state machine, default state, and weight.
- `states` — every state across all layers (name, speed, motion).
- `transitions` — every transition with its conditions, source, and destination.

## How to Call

```bash
unity-mcp-cli run-tool animator-get-data --input '{
  "animatorRef": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool animator-get-data --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool animator-get-data --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `animatorRef` | `string` | Yes | Reference to the AnimatorController asset. The path should start with 'Assets/' and end with '.controller'. RQL: AssetObjectRef |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "animatorRef": {
      "type": "string"
    }
  },
  "required": [
    "animatorRef"
  ]
}
```

## Output

This tool does not return structured output.

