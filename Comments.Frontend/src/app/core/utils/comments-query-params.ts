import { ParamMap } from '@angular/router';

export type CommentSort = 'createdAt' | 'userName' | 'email';
export type CommentViewMode = 'cards' | 'table';

export interface SearchCriteria {
  query: string;
  partial: boolean;
  fields: {
    text: boolean;
    userName: boolean;
  };
  targets: {
    comments: boolean;
    replies: boolean;
  };
}

export interface CommentsQuery {
  search: SearchCriteria | null;
  sort: {
    field: CommentSort;
    descending: boolean;
  };
  viewMode: CommentViewMode;
  cursor: string | null;
}

export const defaultSearchCriteria = (): SearchCriteria => ({
  query: '',
  partial: false,
  fields: { text: true, userName: true },
  targets: { comments: true, replies: true },
});

export const defaultCommentsQuery = (): CommentsQuery => ({
  search: null,
  sort: { field: 'createdAt', descending: true },
  viewMode: 'cards',
  cursor: null,
});

export function parseCommentsQuery(params: Pick<ParamMap, 'get'>): CommentsQuery {
  const query = params.get('q')?.trim() ?? '';
  const search = query
    ? {
        query,
        partial: params.get('partial') === 'true',
        fields: {
          text: params.get('text') !== 'false',
          userName: params.get('user') !== 'false',
        },
        targets: {
          comments: params.get('comments') !== 'false',
          replies: params.get('replies') !== 'false',
        },
      }
    : null;
  const sortParam = params.get('sort');
  const field: CommentSort =
    sortParam === 'userName' || sortParam === 'email' ? sortParam : 'createdAt';

  return {
    search,
    sort: { field, descending: params.get('descending') !== 'false' },
    viewMode: search ? 'cards' : params.get('view') === 'table' ? 'table' : 'cards',
    cursor: params.get('cursor'),
  };
}

export function toCommentsQueryParams(
  query: CommentsQuery,
): Record<string, string | boolean | null> {
  const search = query.search;
  return {
    q: search?.query || null,
    partial: search?.partial ? true : null,
    text: search && !search.fields.text ? false : null,
    user: search && !search.fields.userName ? false : null,
    comments: search && !search.targets.comments ? false : null,
    replies: search && !search.targets.replies ? false : null,
    sort: query.sort.field === 'createdAt' ? null : query.sort.field,
    descending: query.sort.descending ? null : false,
    view: query.viewMode === 'cards' ? null : query.viewMode,
    cursor: query.cursor,
  };
}
