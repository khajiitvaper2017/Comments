import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'highlightSearch', standalone: true, pure: true })
export class HighlightSearchPipe implements PipeTransform {
  transform(html: string, query: string): string {
    const phrase = query.trim();
    if (!phrase) return html;

    const container = document.createElement('div');
    container.innerHTML = html;
    const pattern = new RegExp(`(${escapeRegExp(phrase)})`, 'gi');
    const nodes: Text[] = [];
    const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT);
    let node: Node | null;
    while ((node = walker.nextNode())) nodes.push(node as Text);

    for (const textNode of nodes) {
      if (!pattern.test(textNode.data)) {
        pattern.lastIndex = 0;
        continue;
      }
      pattern.lastIndex = 0;
      const fragment = document.createDocumentFragment();
      let lastIndex = 0;
      for (const match of textNode.data.matchAll(pattern)) {
        const index = match.index ?? 0;
        fragment.append(textNode.data.slice(lastIndex, index));
        const mark = document.createElement('mark');
        mark.textContent = match[0];
        fragment.append(mark);
        lastIndex = index + match[0].length;
      }
      fragment.append(textNode.data.slice(lastIndex));
      textNode.parentNode?.replaceChild(fragment, textNode);
    }
    return container.innerHTML;
  }
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
