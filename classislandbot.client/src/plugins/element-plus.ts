import ElementPlus, { type ConfigProviderContext } from 'element-plus'
import 'element-plus/theme-chalk/src/dark/css-vars.scss'

export const elementPlusConfig: ConfigProviderContext = {
  namespace: 'el',
}

export function syncElementPlusThemeWithSystem() {
  const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
  const updateTheme = ({ matches }: MediaQueryList | MediaQueryListEvent) => {
    document.documentElement.classList.toggle('dark', matches)
  }

  updateTheme(mediaQuery)
  mediaQuery.addEventListener('change', updateTheme)
}

export default ElementPlus
