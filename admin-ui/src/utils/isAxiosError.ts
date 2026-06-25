/** Returns true if the thrown object has a `payload` field matching ApiError */
export function isAxiosError(err: unknown): err is { payload: unknown; message: string } {
  return (
    typeof err === 'object' &&
    err !== null &&
    'payload' in err
  )
}
