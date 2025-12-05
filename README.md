# GPU Instancing Toolkit

A flexible Unity instancing system designed for large-scale rendering of many object types with individual transform data.  
Supports custom memory management and per-chunk buffer reuse to reduce allocations and improve performance.  
All classes are designed with a focus on modularity, performance, thread safety, and serving as a solid baseline for custom implementations.

## Features

- 🚀 High-performance, flawless GPU instancing
- 📦 Global memory pooling
- 🧠 Custom buffer management logic
- 🎯 Designed for large, diverse object sets (e.g., prefabs)

## Notes

Missing:
- ❌ Occlusion culling
- ❌ Spatial partitioning
- ❌ This version is not final and needs some cleanup, I will tackle this in the future

## Example / Benchmark

![Benchmark](img/benchmark.png)
![Runtime Demo](img/GPUInstancingRuntime.mp4)

## Requirements

- Unity 2022.3 or newer (project created with Unity 6)
- Compute shader support