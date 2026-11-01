# QuotaPet

QuotaPet 是一个仅面向 Windows 的低帧率桌面宠物，用来查看本机 AI 服务用量。目前只支持以下
两类服务：

- GPT Plus 套餐的五小时额度、每周额度、重置时间与本机 Token 统计。
- DeepSeek API 的余额、消费记录和 Token 使用量。

QuotaPet 不读取或展示 Claude 模型的用量。Claude Code 只作为读取本机 DeepSeek 调用记录的
日志来源：程序识别模型名以 `deepseek` 开头的记录，其他 Claude 模型记录会被忽略。

> QuotaPet 是个人项目，与 OpenAI、DeepSeek 均无官方关联。

## 功能

- 常驻桌面，可拖动，左键打开或隐藏用量面板。
- 右键切换“全部显示”“仅 GPT Plus”“仅 DeepSeek”。
- 读取本机 Codex 会话日志中的 `event_msg/token_count` 事件。
- 每 5 分钟查询 DeepSeek 官方余额接口，记录余额变化产生的消费。
- 导入 DeepSeek 官方导出的费用 CSV 或 ZIP，补齐历史费用。
- 从本机 Claude Code JSONL 日志中统计 DeepSeek Token。
- 支持原创宠物动画；没有素材时自动使用内置占位宠物。

## 运行

发布版位于源码仓库外：

```text
D:\codexProjects\AIHelper\QuotaPet-deploy\win-x64
```

双击 `QuotaPet.App.exe` 即可运行。发布脚本会生成 `win-x64` 自包含版本，
目标电脑不需要另外安装 .NET 8 Desktop Runtime。

开发环境可从源码目录运行：

```powershell
dotnet run --project src\QuotaPet.App -c Release
```

## DeepSeek 设置

1. 右键宠物，选择“设置 DeepSeek API”。
2. 粘贴 API Key 并保存。
3. 密钥保存在当前 Windows 用户的凭据管理器中，不写入设置文件。
4. 首次查询会建立余额记录起点，之后余额减少的部分会记录为消费。

DeepSeek 余额接口只提供当前余额，不提供 API Key 的历史账单。程序可导入开放平台导出的
`cost-*.csv`，或包含这些文件的 ZIP，补齐今日、本周和本月费用。重复导入相同记录会去重。

Claude Code 的本地 JSONL 日志路径为 `%USERPROFILE%\.claude\projects`。程序只读取模型名、
消息 ID、时间戳和 Token 数量，不读取提示词或回答正文。这里统计的是 DeepSeek Token，不代表
QuotaPet 支持 Claude 模型。

## 自定义宠物素材

公开仓库不包含任何宠物图像。程序需要 8 张动画帧：

```text
src\QuotaPet.App\Assets\pet-swim-1.png
src\QuotaPet.App\Assets\pet-swim-2.png
src\QuotaPet.App\Assets\pet-swim-3.png
src\QuotaPet.App\Assets\pet-swim-4.png
src\QuotaPet.App\Assets\pet-rest-1.png
src\QuotaPet.App\Assets\pet-rest-2.png
src\QuotaPet.App\Assets\pet-rest-3.png
src\QuotaPet.App\Assets\pet-rest-4.png
```

规格：

- PNG，透明背景，112 x 112 像素。
- 主体居中，四周约 8 像素留白。
- 同一角色在 8 帧中保持比例、配色和轮廓一致。
- `swim` 表示 AI 正在活动，`rest` 表示空闲。
- 建议使用纯透明或完全不透明的二值 Alpha，便于边缘检查。

可用下面的提示词逐帧生成原创角色，并把括号内容替换成自己的设定：

```text
Create an original 112x112 pixel-art companion pet sprite frame.
Character: [your original character description and colors].
Pose: [swimming or resting, frame 1 of 4].
Style: clean pixel art, consistent palette, full body, centered,
transparent background, 8 px padding, no text, no watermark,
no copyrighted or trademarked character, no background scenery.
Keep the character proportions and outline consistent across all frames.
```

生成后把文件放入上表路径，或直接放入发布目录的 `Assets` 文件夹。也可以保留
`pet.png`、`pet-source.png` 和 `pet-animation-sheet.png` 作为本地设计源图；运行时只读取
8 个动画帧。缺少任意一帧时，程序会显示内置通用宠物，不会崩溃。

所有 `src\QuotaPet.App\Assets\*.png` 已在 `.gitignore` 中排除，只保留在你的电脑上。不要删除
本地素材；公开仓库只是不提交它们。

发布前可检查素材：

```powershell
D:\ANACONDA\python.exe scripts\verify-sprite-alpha.py
```

该命令检查尺寸、文件名、二值 Alpha 和每帧主体连通性。没有任何自定义素材时，它会提示将使用
占位宠物。

请不要生成或分发受版权、商标或其他第三方权利保护的知名角色素材。发布自己的素材前，确认你
拥有相应使用权。

## 发布与测试

```powershell
dotnet test QuotaPet.sln -c Release
dotnet build QuotaPet.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```

`publish.ps1` 会把独立部署版本写入：

```text
D:\codexProjects\AIHelper\QuotaPet-deploy\win-x64
```

该目录位于源码仓库之外。脚本会复制你本地现有的 `pet*.png`，因此部署版可以显示自定义宠物；
这些图片不会进入公开 Git 仓库。

如果本机的 `dotnet test` 因 .NET workload 组件异常退出，可以改用 SDK 内的测试执行器：

```powershell
dotnet exec "$env:ProgramFiles\dotnet\sdk\8.0.202\vstest.console.dll" tests\QuotaPet.Core.Tests\bin\Release\net8.0\QuotaPet.Core.Tests.dll
dotnet exec "$env:ProgramFiles\dotnet\sdk\8.0.202\vstest.console.dll" tests\QuotaPet.App.Tests\bin\Release\net8.0-windows\QuotaPet.App.Tests.dll
```

## 数据与隐私

- 套餐用量来源：`%USERPROFILE%\.codex\sessions` 中的 Token 事件。
- 设置路径：`%LOCALAPPDATA%\QuotaPet\settings.json`。
- DeepSeek Token 来源：`%USERPROFILE%\.claude\projects` 中匹配 `deepseek` 的记录。
- DeepSeek API Key：Windows 凭据管理器，目标名 `QuotaPet.DeepSeekApiKey`。
- 程序不读取 OpenAI 的 `auth.json`、Cookie 或对话正文。
- 程序不启动本地网络服务，也不向第三方上传日志。
- DeepSeek API Key 只发送给 `https://api.deepseek.com/user/balance`。

## 已知限制

- 仅支持 Windows。
- 仅支持 GPT Plus 套餐用量和 DeepSeek API 用量。
- 远程设备或其他电脑产生的数据，必须同步到本机日志后才能显示。
- DeepSeek 历史费用需从开放平台导出后手动导入。
- DeepSeek Token 只覆盖本机仍保留在 Claude Code 日志中的调用。
- 没有自定义素材时，动画使用代码绘制的通用占位宠物。

## 许可

源代码采用 MIT License。宠物图像、角色设计和生成素材不在许可证覆盖范围内，除非文件所属者
另有书面说明。
