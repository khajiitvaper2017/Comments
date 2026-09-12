import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  Captcha,
  CommentFormValue,
  CommentItem,
  CommentPage,
} from '@app/core/models/comment.models';

@Injectable({ providedIn: 'root' })
export class CommentApiService {
  private readonly http = inject(HttpClient);

  getCaptcha() {
    return this.http.get<Captcha>('/api/captcha');
  }

  getAttachmentText(id: string) {
    return this.http.get(`/api/attachments/${id}`, { responseType: 'text' });
  }

  getComments(page: number, sort: string, descending: boolean) {
    const params = new HttpParams()
      .set('page', page)
      .set('sort', sort)
      .set('descending', descending);
    return this.http.get<CommentPage>('/api/comments', { params });
  }

  getReplies(parentId: string) {
    return this.http.get<CommentItem[]>(`/api/comments/${parentId}/replies`);
  }

  searchComments(query: string, page = 1) {
    const params = new HttpParams().set('q', query).set('page', page);
    return this.http.get<CommentPage>('/api/search', { params });
  }

  createComment(value: CommentFormValue, captchaId: string, file?: File) {
    const data = new FormData();
    Object.entries({ ...value, captchaId, parentId: value.parentId || '' }).forEach(([key, item]) =>
      data.append(key, item),
    );
    if (file) data.append('attachments', file);
    return this.http.post<CommentItem>('/api/comments', data);
  }
}
