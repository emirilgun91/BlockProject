---
name: animation-get-data
description: Inspect a Unity `AnimationClip` asset — name, length, frame rate, wrap mode, looping/legacy/humanMotion flags, local bounds, and the full set of float curves, object-reference curves, and events. Pair with 'animation-modify' to write changes back.
metadata:
  generated-by: mcp-plugin-dotnet
---

# Animation / Get Data

Inspect a Unity `AnimationClip` asset. Returns the high-level clip metadata plus the complete set of float curve bindings, object-reference curve bindings, and animation events. Pair with 'animation-modify' to write changes back.

## Inputs

- `animRef` — reference to the `AnimationClip` asset (path must start with `Assets/` and end with `.anim`).

## Returned fields

- `name`, `length`, `frameRate`, `wrapMode`, `isLooping`, `hasGenericRootTransform`, `hasMotionCurves`, `hasMotionFloatCurves`, `hasRootCurves`, `humanMotion`, `legacy`, `localBounds`, `empty`.
- `curveBindings` — float curve bindings (`path`, `propertyName`, `type`, `isPPtrCurve`, `isDiscreteCurve`, `keyframeCount`).
- `objectReferenceBindings` — object-reference curve bindings (same shape as `curveBindings`).
- `events` — animation events (`time`, `functionName`, `intParameter`, `floatParameter`, `stringParameter`).

## How to Call

```bash
unity-mcp-cli run-tool animation-get-data --input '{
  "animRef": "string_value"
}'
```

> For complex input (multi-line strings, code), save the JSON to a file and use:
> ```bash
> unity-mcp-cli run-tool animation-get-data --input-file args.json
> ```
>
> Or pipe via stdin (recommended):
> ```bash
> unity-mcp-cli run-tool animation-get-data --input-file - <<'EOF'
> {"param": "value"}
> EOF
> ```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `animRef` | `string` | Yes | Reference to the animation asset. The path should start with 'Assets/' and end with '.anim'. RQL: AssetObjectRef |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "animRef": {
      "type": "string"
    }
  },
  "required": [
    "animRef"
  ]
}
```

## Output

This tool does not return structured output.

