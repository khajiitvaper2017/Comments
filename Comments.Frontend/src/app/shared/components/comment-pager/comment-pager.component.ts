import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-comment-pager',
  standalone: true,
  templateUrl: './comment-pager.component.html',
})
export class CommentPagerComponent {
  @Input() total = 0;
  @Input() page = 1;
  @Input() pageSize = 25;
  @Output() readonly pageChanged = new EventEmitter<number>();

  protected get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / Math.max(1, this.pageSize)));
  }

  protected get pageItems(): Array<number | 'ellipsis'> {
    const lastPage = this.pageCount;
    if (lastPage <= 7) return Array.from({ length: lastPage }, (_, index) => index + 1);

    const items: Array<number | 'ellipsis'> = [1];
    if (this.page > 3) items.push('ellipsis');

    const firstVisible = Math.max(2, this.page - 1);
    const lastVisible = Math.min(lastPage - 1, this.page + 1);
    for (let current = firstVisible; current <= lastVisible; current++) items.push(current);

    if (lastVisible < lastPage - 1) items.push('ellipsis');
    items.push(lastPage);
    return items;
  }
}
