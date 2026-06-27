import { defineConfig } from '@alova/wormhole'

const operationIds: Record<string, string> = {
  'GET /api/v1/discussions': 'list',
  'POST /api/v1/discussions': 'create',
  'GET /api/v1/discussions/{id}': 'get',
  'PUT /api/v1/discussions/{id}': 'update',
  'DELETE /api/v1/discussions/{id}': 'delete',
  'GET /api/v1/auth/login': 'login',
  'GET /api/v1/auth/me': 'me',
  'POST /api/v1/auth/logout': 'logout',
  'GET /api/v1/github/repositories': 'getRepositories',
  'GET /api/v1/github/repositories/{repoId}/issues': 'getIssues',
  'GET /api/v1/github/discussions': 'getDiscussions',
}

const swaggerUrl = process.env.SWAGGER_URL ?? 'http://localhost:5086/swagger/v1/swagger.json'

export default defineConfig({
  generator: [
    {
      input: swaggerUrl,
      output: 'src/api',
      responseMediaType: 'application/json',
      bodyMediaType: 'application/json',
      type: 'typescript',
      version: 3,
      global: 'Apis',
      useImportType: true,
      handleApi(apiDescriptor) {
        const operationId = operationIds[`${apiDescriptor.method.toUpperCase()} ${apiDescriptor.url}`]
        if (operationId) {
          apiDescriptor.operationId = operationId
        }

        return apiDescriptor
      },
    },
  ],
  autoUpdate: false,
})
