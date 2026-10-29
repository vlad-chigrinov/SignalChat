import { afterEach, describe, expect, it, vi } from 'vitest'

import { ApiError, requestJson } from '../http'

function stubFetch(response: Response): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => response),
  )
}

describe('requestJson error mapping', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('takes the stable code from a ProblemDetails body', async () => {
    stubFetch(
      new Response(JSON.stringify({ title: 'Conflict', status: 409, code: 'room_name_taken' }), {
        status: 409,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    )

    const error = await requestJson('/api/rooms', { method: 'POST' }).catch(
      (caught: unknown) => caught,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, code: 'room_name_taken' })
  })

  it('gives a null code when the body is not JSON', async () => {
    stubFetch(new Response('<html>Bad Gateway</html>', { status: 502 }))

    const error = await requestJson('/api/rooms').catch((caught: unknown) => caught)

    expect(error).toMatchObject({ status: 502, code: null })
  })

  it('gives a null code when the JSON has no code field', async () => {
    stubFetch(new Response(JSON.stringify({ title: 'Oops' }), { status: 500 }))

    const error = await requestJson('/api/rooms').catch((caught: unknown) => caught)

    expect(error).toMatchObject({ status: 500, code: null })
  })

  it('ignores an empty or non-string code', async () => {
    stubFetch(new Response(JSON.stringify({ code: 42 }), { status: 400 }))

    const error = await requestJson('/api/rooms').catch((caught: unknown) => caught)

    expect(error).toMatchObject({ code: null })
  })

  it('returns the body of a successful answer', async () => {
    stubFetch(new Response(JSON.stringify([{ id: 'a' }]), { status: 200 }))

    await expect(requestJson<{ id: string }[]>('/api/rooms')).resolves.toEqual([{ id: 'a' }])
  })
})
