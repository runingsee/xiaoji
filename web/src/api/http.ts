import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios'
import { showToast } from 'vant'
import { auth, clearAuth } from '../store/auth'

// baseURL=/api：开发经 Vite 代理，生产同源部署，无需跨界
const instance: AxiosInstance = axios.create({ baseURL: '/api', timeout: 90000 })

instance.interceptors.request.use((cfg) => {
  if (auth.token) cfg.headers.Authorization = `Bearer ${auth.token}`
  return cfg
})

instance.interceptors.response.use(
  (resp) => resp.data,
  (err) => {
    const status = err.response?.status
    const msg = err.response?.data?.message
    if (status === 401) {
      clearAuth()
      location.hash = '#/login'
    }
    showToast(msg || (status ? `请求失败(${status})` : '网络异常'))
    return Promise.reject(err)
  },
)

/** 已解包数据的 HTTP 客户端：泛型 T = 接口返回的业务数据（而非 AxiosResponse）。 */
export interface HttpClient {
  get<T = unknown>(url: string, config?: AxiosRequestConfig): Promise<T>
  post<T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>
  patch<T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T>
  delete<T = unknown>(url: string, config?: AxiosRequestConfig): Promise<T>
}

const http: HttpClient = {
  get: (url, config) => instance.get(url, config).then((r) => r.data),
  post: (url, data, config) => instance.post(url, data, config).then((r) => r.data),
  patch: (url, data, config) => instance.patch(url, data, config).then((r) => r.data),
  delete: (url, config) => instance.delete(url, config).then((r) => r.data),
}

export default http