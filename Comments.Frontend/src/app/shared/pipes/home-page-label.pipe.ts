import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'homePageLabel',
  standalone: true,
})
export class HomePageLabelPipe implements PipeTransform {
  transform(value: string): string {
    try {
      return new URL(value).hostname.replace(/^www\./i, '');
    } catch {
      return value
        .replace(/^https?:\/\//i, '')
        .split('/')[0]
        .replace(/^www\./i, '');
    }
  }
}
