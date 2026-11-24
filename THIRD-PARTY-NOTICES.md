# Third-party notices

The Fuel Assistance Intake app ships these open-source components in its app folder.

| Component | Used for | License |
| --- | --- | --- |
| [RapidOcrNet](https://github.com/BobLd/RapidOcrNet) 4.2.0 | Reading scanned paper applications | Apache-2.0 |
| [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) PP-OCRv5 models (in `models\v5`) | Text detection and recognition | Apache-2.0 |
| [ONNX Runtime](https://github.com/microsoft/onnxruntime) | Running the PaddleOCR models | MIT |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | Images for the text reader | MIT |
| [Clipper2](https://github.com/AngusJohnson/Clipper2) | Text box geometry (RapidOcrNet) | Boost Software License 1.0 |

RapidOcrNet's notice:

> Based on source code originally developed in the RapidOCR project (Apache-2.0 license), https://github.com/RapidAI/RapidOCR.
> Uses parts of source code originally developed in the PdfPig project (Apache-2.0 license), https://github.com/UglyToad/PdfPig.
> The dependency on OpenCV was removed thanks to the PContour library and its C# port (https://github.com/LingDong-/PContour,
> https://github.com/BobLd/PContourNet).
> The models made available are from the PaddleOCR project (Apache-2.0 license).
