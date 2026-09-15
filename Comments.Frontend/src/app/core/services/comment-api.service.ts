import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Apollo } from 'apollo-angular';
import { map } from 'rxjs';
import {
  COMMENTS_QUERY,
  CommentsQuery,
  AncestorsQuery,
  ANCESTORS_QUERY,
  RepliesQuery,
  REPLIES_QUERY,
  SEARCH_QUERY,
  SearchQuery,
} from '@app/core/graphql/comment-queries';
import {
  Captcha,
  CommentFormValue,
  CommentItem,
  CommentPage,
} from '@app/core/models/comment.models';

@Injectable({ providedIn: 'root' })
export class CommentApiService {
  private readonly http = inject(HttpClient);
  private readonly apollo = inject(Apollo);

  getCaptcha() {
    return this.http.get<Captcha>('/api/captcha');
  }

  getAttachmentText(id: string) {
    return this.http.get(`/api/attachments/${id}`, { responseType: 'text' });
  }

  getComments(page: number, sort: string, descending: boolean) {
    return this.apollo
      .query<CommentsQuery>({
        query: COMMENTS_QUERY,
        variables: { page, sort, descending },
      })
      .pipe(map((result) => result.data!.comments));
  }

  getReplies(parentId: string) {
    return this.apollo
      .query<RepliesQuery>({
        query: REPLIES_QUERY,
        variables: { parentId },
      })
      .pipe(map((result) => result.data!.replies));
  }

  getAncestors(ids: string[]) {
    return this.apollo
      .query<AncestorsQuery>({ query: ANCESTORS_QUERY, variables: { ids } })
      .pipe(map((result) => result.data!.ancestors));
  }

  searchComments(
    query: string,
    page = 1,
    partial = false,
    searchText = true,
    searchUserName = true,
  ) {
    return this.apollo
      .query<SearchQuery>({
        query: SEARCH_QUERY,
        variables: { query, page, partial, searchText, searchUserName },
      })
      .pipe(map((result) => result.data!.search));
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
