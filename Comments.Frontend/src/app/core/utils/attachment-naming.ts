const imageExtensions: Record<string, string> = {
  'image/webp': '.webp',
  'image/png': '.png',
  'image/jpeg': '.jpg',
  'image/gif': '.gif',
};

export function imageDownloadName(name: string, contentType: string): string {
  const extension = imageExtensions[contentType.toLowerCase()] || '.jpg';
  const fileName = name.split('.').slice(0, -1).join('.');
  return `${fileName}${extension}`;
}
