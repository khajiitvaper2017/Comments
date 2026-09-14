import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { InMemoryCache } from '@apollo/client';
import { provideApollo } from 'apollo-angular';
import { HttpLink } from 'apollo-angular/http';
import { inject } from '@angular/core';
import { App } from '@app/app';
import { appRoutes } from '@app/app.routes';

bootstrapApplication(App, {
  providers: [
    provideHttpClient(),
    provideRouter(appRoutes),
    provideApollo(() => {
      const httpLink = inject(HttpLink);
      return {
        link: httpLink.create({ uri: '/graphql' }),
        cache: new InMemoryCache(),
        defaultOptions: {
          query: {
            fetchPolicy: 'network-only',
          },
        },
      };
    }),
  ],
}).catch((err) => console.error(err));
