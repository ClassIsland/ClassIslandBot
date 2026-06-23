此文件解释 Visual Studio 如何创建项目。

以下工具用于生成此项目:
- create-vite

以下为生成此项目的步骤:
- 使用 create-vite: `npm init --yes vue@latest khaslanamarketplace.client -- --eslint  --typescript ` 创建 vue 项目。
- 更新 `vite.config.ts` 以设置代理和证书。
- 添加 `@type/node` 以进行 `vite.config.js` 输入。
- 更新 `HelloWorld` 组件以提取并显示天气信息。
- 为基本类型添加 `shims-vue.d.ts`。
- 创建项目文件 (`khaslanamarketplace.client.esproj`)。
- 创建 `launch.json` 以启用调试。
- 向解决方案添加项目。
- 将代理终结点更新为后端服务器终结点。
- 将项目添加到启动项目列表。
- 写入此文件。
