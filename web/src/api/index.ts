import http from './http'

// ---------------- 认证
export interface UserDto {
  id: number
  phone: string
  nickname: string | null
  plan: string
  planExpires: string | null
  createdAt: string
}

export interface AuthResponse {
  token: string
  user: UserDto
}

export const authApi = {
  register: (payload: { phone: string; password: string; nickname?: string }) =>
    http.post<AuthResponse>('/auth/register', payload),
  login: (payload: { phone: string; password: string }) =>
    http.post<AuthResponse>('/auth/login', payload),
}

// ---------------- 口述记录
export interface EntryDto {
  id: number
  type: 'expense' | 'income' | 'event' | 'note'
  title: string
  amount: number | null
  category: string | null
  occurredAt: string | null
  rawText: string
  summary: string | null
  status: string
  createdAt: string
}

export interface MissingField {
  field: string
  question: string
}

export interface ParseResult {
  type: string
  title: string
  amount: number | null
  category: string | null
  occurredAt: string | null
  summary: string
  missing: MissingField[]
}

export interface ParseResponse {
  entry: EntryDto | null
  needsConfirm: boolean
  reason: 'missing' | 'parse_failed' | null
  questions: MissingField[] | null
  draftToken: string | null
  draft: ParseResult | null
  hint: string | null
}

// 批量解析：单条结果与整体响应
export interface BatchParseResult {
  entry: EntryDto | null
  needsConfirm: boolean
  reason: string | null
  questions: MissingField[] | null
  draftToken: string | null
  draft: ParseResult | null
  hint: string | null
}

export interface BatchParseResponse {
  batchResults: BatchParseResult[]
  totalCount: number
  hint: string | null
}

export interface MonthSummary {
  month: string
  income: number
  expense: number
  balance: number
}

export const entryApi = {
  parse: (rawText: string) => http.post<ParseResponse>('/entries/parse', { rawText }),
  batch: (rawText: string) => http.post<BatchParseResponse>('/entries/batch', { rawText }),
  confirm: (payload: {
    draftToken?: string
    entryId?: number
    answers?: { field: string; answer: string }[]
    clarification?: string
  }) => http.post<ParseResponse>('/entries/confirm', payload),
  list: (params: { type?: string; month?: string; q?: string; tagId?: number }) =>
    http.get<EntryDto[]>('/entries', { params }),
  search: (query: string) => http.post<EntryDto[]>('/entries/search', { query }),
  summary: (month?: string) =>
    http.get<MonthSummary>('/entries/summary', { params: month ? { month } : {} }),
}

// ---------------- 标签
export interface TagDto {
  id: number
  name: string
  createdAt: string
}

export interface TagStatsDto {
  tagName: string
  count: number
  totalAmount: number
  type: 'income' | 'expense'
}

export const tagApi = {
  list: () => http.get<TagDto[]>('/tags'),
  create: (name: string) => http.post<TagDto>('/tags', { name }),
  remove: (id: number) => http.delete<void>(`/tags/${id}`),
  addToEntry: (entryId: number, tagIds: number[]) =>
    http.post<void>(`/tags/entry/${entryId}/tags`, { tagIds }),
  getEntryTags: (entryId: number) => http.get<TagDto[]>(`/tags/entry/${entryId}/tags`),
  removeTagFromEntry: (entryId: number, tagId: number) =>
    http.delete<void>(`/tags/entry/${entryId}/tags/${tagId}`),
  stats: (month?: string) =>
    http.get<TagStatsDto[]>('/tags/stats', { params: month ? { month } : {} }),
}

// ---------------- 对话咨询
export interface AskResponse {
  answer: string
}

export const askApi = {
  ask: (question: string) => http.post<AskResponse>('/ask', { question }),
}