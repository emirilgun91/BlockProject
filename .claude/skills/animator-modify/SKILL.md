---
name: animator-modify
description: Apply a batch of modifications to a Unity `AnimatorController` — add/remove parameters, layers, states, and transitions; set default states; set state motion/speed. Use 'animator-get-data' first to discover valid names.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Animator / Modify

Apply a batch of modifications to a Unity `AnimatorController` asset. Each modification is dispatched by its `AnimatorModificationType` discriminator. Use 'animator-get-data' first to discover valid names so the diff is targeted.

## Inputs

- `animatorRef` — reference to the `AnimatorController` asset (path must start with `Assets/` and end with `.controller`).
- `modifications` — array of `AnimatorModification` entries.

## Supported modification types

- `AddParameter` / `RemoveParameter` — manage controller parameters (float, int, bool, trigger).
- `AddLayer` / `RemoveLayer` — manage animator layers.
- `AddState` / `RemoveState` — manage states inside a specific layer.
- `SetDefaultState` — pick the default state for a layer.
- `AddTransition` / `RemoveTransition` — manage state-to-state transitions.
- `AddAnyStateTransition` — add a transition from Any State.
- `SetStateMotion` — assign an `AnimationClip` as a state's motion.
- `SetStateSpeed` — set a state's speed multiplier.

## Behavior

Per-modification errors are accumulated in the response's `errors` array instead of aborting the whole batch. The controller asset is marked dirty and saved after the modifications complete.

## How to Call

```bash
unity-mcp-cli run-tool animator-modify --input '{
  "animatorRef": "string_value",
  "modifications": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool animator-modify --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool animator-modify --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `animatorRef` | `string` | Yes | Reference to the AnimatorController asset to modify. RQL: AssetObjectRef |
| `modifications` | `string` | Yes | Array of modifications to apply to the controller. RQL: [AnimatorModification] |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "animatorRef": {
      "type": "string"
    },
    "modifications": {
      "type": "string"
    }
  },
  "required": [
    "animatorRef",
    "modifications"
  ]
}
```

## Output

This tool does not return structured output.

