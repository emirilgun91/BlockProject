---
name: animation-modify
description: Apply a batch of modifications to a Unity `AnimationClip` — set/remove float curves, clear all curves, set frame rate / wrap mode / legacy flag, add or clear animation events. Use 'animation-get-data' first to discover valid curve bindings.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Animation / Modify

Apply a batch of modifications to a Unity `AnimationClip` asset. Each modification is dispatched by its `ModificationType` discriminator. Use 'animation-get-data' first to discover valid property names and existing curves so the diff is targeted.

## Inputs

- `animRef` — reference to the `AnimationClip` asset (path must start with `Assets/` and end with `.anim`).
- `modifications` — array of `AnimationModification` entries.

## Supported modification types

- `SetCurve` — add or replace a float animation curve (`path`, `propertyName`, `type`, `keyframes`).
- `RemoveCurve` — remove a specific binding.
- `ClearCurves` — remove every curve from the clip.
- `SetFrameRate` — set the clip's frame rate.
- `SetWrapMode` — set the clip's `WrapMode`.
- `SetLegacy` — toggle the legacy animation flag.
- `AddEvent` — append an animation event (`time`, `functionName`, `intParameter`, `floatParameter`, `stringParameter`).
- `ClearEvents` — remove every animation event.

## Behavior

Per-modification errors are accumulated in the response's `errors` array instead of aborting the whole batch. Events are applied as a single rewrite after all per-entry mutations finish.

## How to Call

```bash
unity-mcp-cli run-tool animation-modify --input '{
  "animRef": "string_value",
  "modifications": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool animation-modify --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool animation-modify --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `animRef` | `string` | Yes | Reference to the AnimationClip asset to modify. RQL: AssetObjectRef |
| `modifications` | `string` | Yes | Array of modifications to apply to the clip. RQL: [AnimationModification] |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "animRef": {
      "type": "string"
    },
    "modifications": {
      "type": "string"
    }
  },
  "required": [
    "animRef",
    "modifications"
  ]
}
```

## Output

This tool does not return structured output.

