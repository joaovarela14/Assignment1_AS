import http from 'k6/http';
import { check, group, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 5 },
    { duration: '1m', target: 10 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<1500'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost';
const SEARCH_TERM = __ENV.SEARCH_TERM || 'laptop';
const ZERO_RESULT_TERM = __ENV.ZERO_RESULT_TERM || 'zzzz-no-product-123';
const PRODUCT_PATHS = (__ENV.PRODUCT_PATHS || '/asus-laptop,/lenovo-thinkpad-carbon-laptop')
  .split(',')
  .map((path) => path.trim())
  .filter(Boolean);

export default function () {
  group('catalog search and product view', () => {
    const home = http.get(`${BASE_URL}/`);
    check(home, {
      'home page responded': (response) => response && response.status === 200,
    });

    const search = http.get(`${BASE_URL}/search?q=${encodeURIComponent(SEARCH_TERM)}`);
    check(search, {
      'search page responded': (response) => response && response.status === 200,
      'search response contains a known product': (response) =>
        !!response?.body && PRODUCT_PATHS.some((path) => response.body.includes(path)),
    });

    const productPath = PRODUCT_PATHS[Math.floor(Math.random() * PRODUCT_PATHS.length)];
    const product = http.get(`${BASE_URL}${productPath}`);
    check(product, {
      'product page responded': (response) => response && response.status === 200,
    });
  });

  if (__ITER % 5 === 0) {
    const zeroResultsSearch = http.get(`${BASE_URL}/search?q=${encodeURIComponent(ZERO_RESULT_TERM)}`);
    check(zeroResultsSearch, {
      'zero-results search responded': (response) => response && response.status === 200,
    });
  }

  sleep(Math.random() * 2 + 0.5);
}
