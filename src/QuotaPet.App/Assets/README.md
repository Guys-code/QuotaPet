# 自定义宠物素材

QuotaPet 不随公开仓库分发宠物图像。请在这里放入自己生成或拥有使用权的素材：

```text
pet-swim-1.png
pet-swim-2.png
pet-swim-3.png
pet-swim-4.png
pet-rest-1.png
pet-rest-2.png
pet-rest-3.png
pet-rest-4.png
```

每张图片应为 112 x 112 像素、透明背景 PNG，主体尽量居中并保留约 8 像素边距。
`pet.png`、`pet-source.png` 和 `pet-animation-sheet.png` 可保存设计源图，但程序运行时只读取
上面 8 个动画帧。

缺少任意一帧时，程序会显示代码绘制的通用占位宠物，不会崩溃。所有 `*.png` 已被 `.gitignore`
排除，不会误提交到公开仓库。

请使用原创角色，或确保你对所用角色和图像拥有公开分发权。不要生成或分发受版权、商标保护的
第三方角色素材。
