import { Routes } from '@angular/router';

export const appRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('@app/pages/comments/comments-page.component').then((m) => m.CommentsPageComponent),
  },
  { path: '**', redirectTo: '' },
];
