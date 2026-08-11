import axios, { type AxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import router from '@/router'

export const TOKEN_KEY = 'admin_token'

/** 后端统一信封 { code, data, message }，code=0 成功。 */
export interface ApiEnvelope<T = unknown> {
  code: number
  data: T
  message: string
}

const http = axios.create({
  baseURL: '/api/v1/admin',
  timeout: 60000,
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (response) => {
    const envelope = response.data as ApiEnvelope
    if (envelope && typeof envelope === 'object' && 'code' in envelope) {
      if (envelope.code === 0) {
        return envelope.data as never
      }
      if (envelope.code === 401) {
        localStorage.removeItem(TOKEN_KEY)
        if (router.currentRoute.value.path !== '/login') {
          ElMessage.error('登录已过期，请重新登录')
          router.push('/login')
        }
      } else {
        ElMessage.error(envelope.message || '请求失败')
      }
      return Promise.reject(new Error(envelope.message || `code=${envelope.code}`))
    }
    return envelope as never
  },
  (error) => {
    ElMessage.error(error?.message || '网络错误，请检查服务是否可用')
    return Promise.reject(error)
  },
)

/** 类型化请求：直接返回信封中的 data。 */
export function request<T>(config: AxiosRequestConfig): Promise<T> {
  return http.request(config) as Promise<T>
}

// ================= 文件下载（不套信封，返回 Blob） =================

const httpDownload = axios.create({
  baseURL: '/api/v1/admin',
  timeout: 60000,
  responseType: 'blob',
})

httpDownload.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

httpDownload.interceptors.response.use(
  (response) => response.data as never,
  (error) => {
    ElMessage.error(error?.message || '网络错误，请检查服务是否可用')
    return Promise.reject(error)
  },
)

/** 下载接口：返回 Blob（用于导出等直接返回文件的接口）。 */
export function download<T = Blob>(config: AxiosRequestConfig): Promise<T> {
  return httpDownload.request(config) as Promise<T>
}

export default http
