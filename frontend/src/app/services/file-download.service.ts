import { Injectable } from '@angular/core';

/** Liten hjälptjänst för att trigga nedladdning av en Blob i webbläsaren. */
@Injectable({ providedIn: 'root' })
export class FileDownloadService {
  download(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
