import { createApp } from 'vue'
import { createPinia } from 'pinia'

import './assets/main.css'
import App from './App.vue'
import router from './router'

const app = createApp(App)

// Pinia first: the router guard reads the auth store.
app.use(createPinia())
app.use(router)

app.mount('#app')
