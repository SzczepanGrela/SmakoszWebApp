# Third-Party Notices

This repository contains code and assets that originate from third-party
projects. Each item below is included under its respective license. The
notices preserved here satisfy the attribution and license-text inclusion
requirements of those licenses.

CDN-loaded libraries (Bootstrap, Font Awesome, Cloudflare Turnstile) and
packages installed via NuGet are not listed here — their licenses ship
with the packages themselves.

---

## 1. Wolt blurhash

- File: `src/Smakosz.Client/wwwroot/js/blurhash.js`
- Source: https://github.com/woltapp/blurhash
- License: MIT

```
MIT License

Copyright (c) 2018 Wolt Enterprises

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 2. prometheus-net Grafana dashboards

- File: `infra/grafana/dashboards/aspnetcore.json`
- Source: https://github.com/prometheus-net/grafana-dashboards
  (originally published as dashboard ID 10915 on https://grafana.com/grafana/dashboards/)
- License: MIT

```
MIT License

Copyright (c) 2020 prometheus-net contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 3. Microsoft Blazor WASM PWA template

- File: `src/Smakosz.Client/wwwroot/service-worker.published.js`
- Source: ASP.NET Core Blazor WebAssembly PWA template
  (https://github.com/dotnet/aspnetcore)
- License: MIT

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 4. Node Exporter Grafana dashboard

- File: `infra/grafana/dashboards/node.json`
- Source: https://github.com/starsliao/Prometheus
  (originally published as dashboard ID 11074 on https://grafana.com/grafana/dashboards/)
- License: Apache License 2.0

```
                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

   Copyright (c) starsliao

   Notice for users: this dashboard JSON has been imported and the datasource
   UID placeholder substituted from "${DS__VICTORIAMETRICS}" to "Prometheus" to
   match our local Grafana provisioning. No other modifications were made to
   the dashboard structure or panel queries.
```

---

## 5. HerBERT base cased

- File: loaded at runtime by `gpu-worker/inference/text_moderator.py`
- Source: https://huggingface.co/allegro/herbert-base-cased
- License: CC BY 4.0
- Paper: Mroczkowski R., Rybak P., Wroblewska A., Gawlik I. (2021).
  HerBERT: Efficiently Pretrained Transformer-based Language Model for Polish.
  Proceedings of the 8th Workshop on Balto-Slavic Natural Language Processing.

```
Creative Commons Attribution 4.0 International License (CC BY 4.0)

You are free to share and adapt the material in any medium or format for any
purpose, even commercially, under the following terms:

Attribution: You must give appropriate credit, provide a link to the license,
and indicate if changes were made. You may do so in any reasonable manner,
but not in any way that suggests the licensor endorses you or your use.

No additional restrictions: You may not apply legal terms or technological
measures that legally restrict others from doing anything the license permits.

Full license text: https://creativecommons.org/licenses/by/4.0/legalcode
```

Notice for users: this project fine-tunes HerBERT on a private toxicity
dataset and stores the resulting weights in Cloudflare R2 under
`r2://smakosz-models/herbert/`. The base model attribution above applies to
the unmodified pretrained weights pulled from HuggingFace as fallback.

---

## 6. OpenAI CLIP ViT-B/32

- File: loaded at runtime by `gpu-worker/inference/image_moderator.py`
- Source: https://huggingface.co/openai/clip-vit-base-patch32
- License: MIT
- Paper: Radford A., Kim J. W., Hallacy C., et al. (2021).
  Learning Transferable Visual Models From Natural Language Supervision.
  International Conference on Machine Learning (ICML 2021).

```
MIT License

Copyright (c) 2021 OpenAI

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 7. Falconsai NSFW image detection

- File: loaded at runtime by `gpu-worker/inference/image_moderator.py`
- Source: https://huggingface.co/Falconsai/nsfw_image_detection
- License: Apache License 2.0
- Architecture: fine-tuned `google/vit-base-patch16-224-in21k` (Vision Transformer)
  on a curated NSFW classification dataset.

```
                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

   Copyright (c) Falconsai
```

Notice for users: weights are pulled at runtime from HuggingFace and cached
locally in `model_cache/hf/` inside the gpu-worker container. The model is
used unmodified; the project applies a substring match on the model's
`id2label` dictionary to extract the NSFW probability.
