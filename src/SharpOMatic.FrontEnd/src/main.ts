import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/components/app/app.config';
import { App } from './app/components/app/app';

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
