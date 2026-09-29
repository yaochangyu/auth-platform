import { computed, onUnmounted, ref } from 'vue'

export function useCountdown() {
  const targetTime = ref<number | null>(null)
  const remainingMs = ref(0)
  let timer: ReturnType<typeof setInterval> | null = null

  function tick() {
    if (targetTime.value === null) {
      return
    }

    const remaining = targetTime.value - Date.now()
    if (remaining <= 0) {
      remainingMs.value = 0
      stop()
      return
    }

    remainingMs.value = remaining
  }

  function start(untilIso: string) {
    stop()
    targetTime.value = new Date(untilIso).getTime()
    tick()
    timer = setInterval(tick, 1000)
  }

  function stop() {
    if (timer !== null) {
      clearInterval(timer)
      timer = null
    }
    targetTime.value = null
  }

  const isRunning = computed(() => remainingMs.value > 0)

  const label = computed(() => {
    const totalSeconds = Math.ceil(remainingMs.value / 1000)
    const minutes = Math.floor(totalSeconds / 60)
    const seconds = totalSeconds % 60
    return `${minutes} 分 ${seconds} 秒`
  })

  onUnmounted(stop)

  return { isRunning, label, start, stop }
}
