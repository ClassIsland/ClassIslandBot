<script setup lang="ts">

</script>

<template>
  <div>
    <div
      class="relative h-screen w-screen overflow-hidden
             bg-[linear-gradient(306deg,#0B0411_0%,#7E3D88_100%)]
             before:absolute before:inset-0 before:content-['']
             before:bg-[linear-gradient(to_right,rgba(255,255,255,.08)_1px,transparent_1px),linear-gradient(to_bottom,rgba(255,255,255,.08)_1px,transparent_1px)]
             before:bg-size-[32px_32px]
             before:pointer-events-none"
    >
      <img
        src="@/assets/Cyrene_Gift_of_Odyssey_3.7.webp"
        class="relative z-10 h-full w-full object-contain"
      />

      <div class="auth-theme-panel" >
        <div class="flex items-center gap-2 mx-6 my-4">
          <img src="@/assets/Logo.png" width="32" height="32"
               alt="Logo"/>
          <h1 class="font-medium text-xl">ClassIsland Bot</h1>
        </div>
        <router-view/>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.auth-theme-panel {
  position: absolute;
  top: 0;
  right: 0;
  z-index: 20;
  width: min(520px, 100vw);
  height: 100%;
  background: var(--el-bg-color);
  background: color-mix(in srgb, var(--el-bg-color) 86%, transparent);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);

  display: grid;
  grid-template-rows: auto 1fr auto;
}

@media (max-width: 520px) {
  .auth-theme-panel {
    backdrop-filter: none;
    -webkit-backdrop-filter: none;
    background: var(--el-bg-color) !important;
  }
}
</style>
