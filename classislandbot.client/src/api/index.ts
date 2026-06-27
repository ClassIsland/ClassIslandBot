import { createAlova } from 'alova';
import fetchAdapter from 'alova/fetch';
import { createApis, withConfigType, mountApis } from './createApis';

async function parseResponse(res: Response) {
  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || `${res.status} ${res.statusText}`);
  }

  if (res.status === 204) {
    return null;
  }

  const content = await res.text();
  if (!content) {
    return null;
  }

  const contentType = res.headers.get('content-type') ?? '';
  return contentType.includes('application/json') ? JSON.parse(content) : content;
}

export const alovaInstance = createAlova({
  baseURL: '',
  requestAdapter: fetchAdapter(),
  responded: parseResponse,
  cacheFor: null
});

export const $$userConfigMap = withConfigType({});

const Apis = createApis(alovaInstance, $$userConfigMap);

mountApis(Apis);

export default Apis;
