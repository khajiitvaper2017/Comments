export interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
}

export interface CommentItem {
  id: string;
  parentId?: string;
  userName: string;
  email: string;
  homePage?: string;
  text: string;
  createdAtUtc: string;
  attachments: Attachment[];
  replies: CommentItem[];
  replyCount: number;
  hasMoreReplies?: boolean;
  isSearchMatch?: boolean;
}

export interface CommentPage {
  items: CommentItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  descending: boolean;
  totalReplyCount: number;
}

export interface Captcha {
  id: string;
  imageDataUrl: string;
}

export interface CommentFormValue {
  userName: string;
  email: string;
  homePage: string;
  text: string;
  captchaAnswer: string;
  parentId: string;
}
