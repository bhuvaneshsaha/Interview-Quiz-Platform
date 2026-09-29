import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CORRELATION_HEADER } from './api-error';
import { correlationIdInterceptor } from './correlation-id.interceptor';

describe('correlationIdInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([correlationIdInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('adds X-Correlation-ID when missing', () => {
    http.get('/api/me').subscribe();
    const req = httpMock.expectOne('/api/me');
    const id = req.request.headers.get(CORRELATION_HEADER);
    expect(id).toBeTruthy();
    expect(id).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    );
    req.flush({});
  });

  it('keeps an existing X-Correlation-ID', () => {
    http.get('/api/me', { headers: { [CORRELATION_HEADER]: 'fixed-id' } }).subscribe();
    const req = httpMock.expectOne('/api/me');
    expect(req.request.headers.get(CORRELATION_HEADER)).toBe('fixed-id');
    req.flush({});
  });
});
