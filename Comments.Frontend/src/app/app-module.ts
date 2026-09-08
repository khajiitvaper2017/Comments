import { HttpClientModule } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AppRoutingModule } from './app-routing-module';
import { App } from './app';

@NgModule({
  imports: [BrowserModule, HttpClientModule, AppRoutingModule],
  providers: [provideBrowserGlobalErrorListeners()],
})
export class AppModule {}
