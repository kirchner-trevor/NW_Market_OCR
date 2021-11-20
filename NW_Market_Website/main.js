import App from './app.js'
import Home from './home.js'
import NotFound from './not-found.js'

const routes = [
  { path: '/', redirect: '/Orofena/' },
  { path: '/:server/', component: Home, props: true },
  { path: '*', component: NotFound }
]

const router = new VueRouter({
  routes
})

new Vue({
  router,
  render: h => h(App)
}).$mount('#app')