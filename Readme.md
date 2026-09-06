# Lorenz

A GPU-accelerated renderer for the Lorenz attractor. A million particles are integrated on the GPU with [ILGPU](https://github.com/m4rs-mt/ILGPU), splatted into a density buffer, bloomed, and piped straight to `ffmpeg` as an H.264 video.

![Lorenz attractor](example.gif)

Each frame runs a small kernel pipeline:

1. Integrate: forward Euler on the Lorenz system, 8 substeps per frame.
2. Raster: every substep projects all particles through the view-projection matrix and atomically accumulates into three float density buffers. Accumulating across substeps is what produces the motion trails.
3. Color: particles are tinted by the magnitude of the local flow vector, blue (slow) through red (fast).
4. Bloom: threshold, separable Gaussian blur, then combine with the density and tone-map through `v / (1 + v)`.
5. Output: a faint ground grid is drawn on the CPU for spatial reference, and the RGBA frame is written to an `ffmpeg` pipe.

There is no depth buffer and no sorting; density accumulation is order-independent, which is what makes the whole thing a handful of trivially parallel kernels.
