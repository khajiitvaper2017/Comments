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
  isSearchMatch?: boolean;
  ancestorIds?: string[];
}

export interface CommentPage {
  items: CommentItem[];
  nextCursor?: string | null;
  sort: string;
  descending: boolean;
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
