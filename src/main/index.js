'use strict'

import path from 'path'
import fs from 'fs'
// import os from 'os'
import { app, BrowserWindow } from 'electron'

/**
 * Set `__static` path to static files in production
 * https://simulatedgreg.gitbooks.io/electron-vue/content/en/using-static-assets.html
 */
if (process.env.NODE_ENV !== 'development') {
  global.__static = require('path')
    .join(__dirname, '/static')
    .replace(/\\/g, '\\\\')
}
let mainWindow
const winURL =
  process.env.NODE_ENV === 'development'
    ? `http://localhost:9080`
    : `file://${__dirname}/index.html`

function createWindow() {
  /**
   * Initial window options
   */
  mainWindow = new BrowserWindow({
    height: 563,
    useContentSize: true,
    width: 1000
  })

  mainWindow.loadURL(winURL)
  mainWindow.maximize()
  mainWindow.on('closed', () => {
    mainWindow = null
  })
}
let apiDir = path.resolve(__dirname, '../api/FastSQL.API/')
if (process.env.NODE_ENV !== 'development') {
  apiDir = path.resolve(__dirname, '../dist/api/bin/dist/win')
}
let envName = process.env.NODE_ENV[0].toUpperCase() + process.env.NODE_ENV.slice(1);
let confFile = `appsettings.${envName}.json`
process.env.API_DIR = apiDir;
process.env.CONFIG_FILE = confFile

var confPath = path.resolve(apiDir, confFile)
var conf = {
  api: {
    port: 7001
  }
}
if (fs.existsSync(confPath)) {
  conf = JSON.parse(fs.readFileSync(confPath, 'utf8'))
  if (!conf.api) {
    conf.api = {
      port: 7001
    }
  }
}

process.env.BACKEND = `http://localhost:${conf.api.port}`

app.on('ready', createWindow)

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow()
  }
})

/**
 * Auto Updater
 *
 * Uncomment the following code below and install `electron-updater` to
 * support auto updating. Code Signing with a valid certificate is required.
 * https://simulatedgreg.gitbooks.io/electron-vue/content/en/using-electron-builder.html#auto-updating
 */

/*
import { autoUpdater } from 'electron-updater'

autoUpdater.on('update-downloaded', () => {
  autoUpdater.quitAndInstall()
})

app.on('ready', () => {
  if (process.env.NODE_ENV === 'production') autoUpdater.checkForUpdates()
})
 */
