import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NgForm } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { CommentFormComponent } from './comment-form.component';

describe('CommentFormComponent', () => {
  let fixture: ComponentFixture<CommentFormComponent>;
  let component: CommentFormComponent;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommentFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(CommentFormComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('does not open CAPTCHA when the form is invalid', () => {
    const form = fixture.debugElement.query(By.css('form')).injector.get(NgForm);

    component.form.userName = 'bad name';
    component.form.email = 'invalid';
    component.form.text = '';
    component.submit(form);

    expect(component.showCaptcha).toBe(false);
  });

  it('opens CAPTCHA after client validation succeeds', () => {
    const form = {
      invalid: false,
      form: { markAllAsTouched: () => undefined },
    } as unknown as NgForm;

    component.form.userName = 'User123';
    component.form.email = 'user@example.com';
    component.form.text = 'A valid comment.';
    component.submit(form);

    const request = http.expectOne('/api/captcha');
    request.flush({ id: 'captcha-id', imageDataUrl: 'data:image/png;base64,test' });
    expect(component.showCaptcha).toBe(true);
  });
});
