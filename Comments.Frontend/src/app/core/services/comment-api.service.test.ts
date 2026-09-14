import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { InMemoryCache } from '@apollo/client';
import { provideApollo } from 'apollo-angular';
import { HttpLink } from 'apollo-angular/http';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CommentPage } from '@app/core/models/comment.models';
import { CommentApiService } from './comment-api.service';

describe('CommentApiService Apollo reads', () => {
  let service: CommentApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApollo(() => {
          const httpLink = TestBed.inject(HttpLink);
          return {
            link: httpLink.create({ uri: '/graphql' }),
            cache: new InMemoryCache(),
          };
        }),
      ],
    });
    service = TestBed.inject(CommentApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads comments through Apollo', async () => {
    const expected: CommentPage = {
      items: [],
      page: 2,
      pageSize: 25,
      totalCount: 0,
      sort: 'userName',
      descending: false,
      totalReplyCount: 0,
    };
    const resultPromise = firstValueFrom(service.getComments(2, 'userName', false));

    const request = http.expectOne('/graphql');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.variables).toEqual({
      page: 2,
      sort: 'userName',
      descending: false,
    });
    expect(request.request.body.operationName).toBe('Comments');
    expect(request.request.body.query).toMatch(/items\s*\{[\s\S]*replies\s*\{/);

    request.flush({ data: { comments: expected } });

    await expect(resultPromise).resolves.toEqual(expected);
  });
});
