# Silero VAD Model

Place `silero_vad.onnx` in this directory to enable the primary speech detector.

Use the helper script from the repo root:

```bash
./scripts/download-silero-model.sh
```

The app falls back to an energy-based detector if the model is missing.
