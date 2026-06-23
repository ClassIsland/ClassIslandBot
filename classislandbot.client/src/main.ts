import './assets/tailwind.css'
import './assets/main.scss'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import App from './App.vue'
import ElementPlus, { elementPlusConfig, syncElementPlusThemeWithSystem } from './plugins/element-plus'
import router from './router'

const pinia = createPinia()

syncElementPlusThemeWithSystem()

createApp(App)
  .use(pinia)
  .use(router)
  .use(ElementPlus, elementPlusConfig)
  .mount('#app')
