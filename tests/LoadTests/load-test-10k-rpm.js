/**
 * Load Test Script for Full Traceability System
 * 
 * Purpose: Validate that the traceability system can handle 10,000 requests per minute
 * without degrading API performance.
 * 
 * Requirements:
 * - System SHALL support logging 10,000 requests per minute without degrading API response times
 * - System SHALL add no more than 10ms latency to API requests for 99% of operations
 * - Audit Logger SHALL use asynchronous writes to avoid blocking API request processing
 * 
 * Prerequisites:
 * - k6 installed (https://k6.io/docs/getting-started/installation/)
 * - ThinkOnErp API running (default: http://localhost:5000)
 * - Valid JWT token for authentication
 * 
 * Usage:
 *   k6 run load-test-10k-rpm.js
 *   k6 run --vus 100 --duration 5m load-test-10k-rpm.js
 *   k6 run --env API_URL=http://your-api-url load-test-10k-rpm.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics for traceability system validation
const auditOverhead = new Trend('audit_overhead_ms', true);
const apiResponseTime = new Trend('api_response_time_ms', true);
const errorRate = new Rate('error_rate');
const requestsPerMinute = new Counter('requests_per_minute');

// Test configuration
export const options = {
    // Scenario 1: Ramp up to 10,000 requests per minute
    scenarios: {
        sustained_load: {
            executor: 'ramping-arrival-rate',
            startRate: 100,  // Start with 100 requests per minute
            timeUnit: '1m',
            preAllocatedVUs: 50,
            maxVUs: 200,
            stages: [
                { duration: '2m', target: 1000 },   // Ramp up to 1,000 req/min
                { duration: '3m', target: 5000 },   // Ramp up to 5,000 req/min
                { duration: '5m', target: 10000 },  // Ramp up to 10,000 req/min (target)
                { duration: '10m', target: 10000 }, // Sustain 10,000 req/min for 10 minutes
                { duration: '2m', target: 1000 },   // Ramp down to 1,000 req/min
                { duration: '1m', target: 0 },      // Ramp down to 0
            ],
        },
    },
    
    // Performance thresholds (requirements validation)
    thresholds: {
        // Requirement: <10ms latency for 99% of operations
        'api_response_time_ms': [
            'p(99)<10',      // 99th percentile should be under 10ms (CRITICAL)
            'p(95)<8',       // 95th percentile should be under 8ms
            'p(50)<5',       // 50th percentile should be under 5ms
        ],
        
        // HTTP request duration (includes network + processing)
        'http_req_duration': [
            'p(99)<500',     // 99% of requests should complete within 500ms
            'p(95)<300',     // 95% of requests should complete within 300ms
            'avg<200',       // Average response time should be under 200ms
        ],
        
        // Error rate should be minimal
        'error_rate': ['rate<0.01'],  // Less than 1% error rate
        
        // HTTP failures should be minimal
        'http_req_failed': ['rate<0.01'],
    },
};

// Environment configuration
const API_URL = __ENV.API_URL || 'http://localhost:5000';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

// Test data generators
function generateCompanyData() {
    return {
        companyName: `LoadTest Company ${Date.now()}`,
        companyNameSecondLanguage: `شركة اختبار ${Date.now()}`,
        address: '123 Test Street',
        addressSecondLanguage: 'شارع الاختبار 123',
        telephone: '+1234567890',
        mobile: '+0987654321',
        fax: '+1122334455',
        email: `loadtest${Date.now()}@example.com`,
        taxNumber: `TAX${Date.now()}`,
        commercialRegistrationNo: `CR${Date.now()}`,
        fiscalYearId: 1,
        defaultBranch: {
            branchName: `Main Branch ${Date.now()}`,
            branchNameSecondLanguage: `الفرع الرئيسي ${Date.now()}`,
            address: '123 Branch Street',
            addressSecondLanguage: 'شارع الفرع 123',
            telephone: '+1234567890',
            mobile: '+0987654321'
        }
    };
}

function generateUserData() {
    const timestamp = Date.now();
    return {
        userName: `loadtest_user_${timestamp}`,
        password: 'LoadTest123!',
        fullName: `Load Test User ${timestamp}`,
        fullNameSecondLanguage: `مستخدم اختبار ${timestamp}`,
        email: `loadtest${timestamp}@example.com`,
        mobile: `+123456${timestamp % 10000}`,
        companyId: 1,
        branchId: 1,
        roleId: 2
    };
}

// Setup function - runs once per VU
export function setup() {
    console.log('=== Load Test Setup ===');
    console.log(`API URL: ${API_URL}`);
    console.log(`Target: 10,000 requests per minute`);
    console.log(`Duration: 23 minutes (ramp up + sustained + ramp down)`);
    console.log('========================');
    
    // Authenticate and get a valid token if not provided
    let token = JWT_TOKEN;
    
    if (!token) {
        console.log('No JWT token provided, attempting to authenticate...');
        const loginResponse = http.post(`${API_URL}/api/auth/login`, JSON.stringify({
            userName: 'superadmin',
            password: 'SuperAdmin123!'
        }), {
            headers: { 'Content-Type': 'application/json' },
        });
        
        if (loginResponse.status === 200) {
            const loginData = JSON.parse(loginResponse.body);
            token = loginData.token;
            console.log('Authentication successful');
        } else {
            console.error('Authentication failed:', loginResponse.status, loginResponse.body);
            throw new Error('Failed to authenticate. Please provide a valid JWT_TOKEN environment variable.');
        }
    }
    
    return { token };
}

// Main test function - runs for each iteration
export default function(data) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
    };
    
    // Mix of different API operations to simulate realistic load
    const operations = [
        testGetCompanies,
        testGetUsers,
        testGetRoles,
        testGetCurrencies,
        testGetBranches,
        testCreateCompany,
        testGetAuditLogs,
    ];
    
    // Randomly select an operation (weighted towards read operations)
    const weights = [30, 25, 15, 10, 10, 5, 5]; // Percentages
    const random = Math.random() * 100;
    let cumulative = 0;
    let selectedIndex = 0;
    
    for (let i = 0; i < weights.length; i++) {
        cumulative += weights[i];
        if (random < cumulative) {
            selectedIndex = i;
            break;
        }
    }
    
    // Execute the selected operation
    operations[selectedIndex](headers);
    
    // Increment requests per minute counter
    requestsPerMinute.add(1);
    
    // Small random sleep to simulate realistic user behavior (0-100ms)
    sleep(Math.random() * 0.1);
}

// Test operations
function testGetCompanies(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/companies`, { headers });
    const endTime = Date.now();
    
    const success = check(response, {
        'GET /api/companies status is 200': (r) => r.status === 200,
        'GET /api/companies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
    
    // Check for audit overhead (if response includes timing headers)
    if (response.headers['X-Audit-Overhead-Ms']) {
        auditOverhead.add(parseFloat(response.headers['X-Audit-Overhead-Ms']));
    }
}

function testGetUsers(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/users`, { headers });
    const endTime = Date.now();
    
    const success = check(response, {
        'GET /api/users status is 200': (r) => r.status === 200,
        'GET /api/users has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
}

function testGetRoles(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/roles`, { headers });
    const endTime = Date.now();
    
    const success = check(response, {
        'GET /api/roles status is 200': (r) => r.status === 200,
        'GET /api/roles has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
}

function testGetCurrencies(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/currencies`, { headers });
    const endTime = Date.now();
    
    const success = check(response, {
        'GET /api/currencies status is 200': (r) => r.status === 200,
        'GET /api/currencies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
}

function testGetBranches(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/branches`, { headers });
    const endTime = Date.now();
    
    const success = check(response, {
        'GET /api/branches status is 200': (r) => r.status === 200,
        'GET /api/branches has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
}

function testCreateCompany(headers) {
    const startTime = Date.now();
    const companyData = generateCompanyData();
    const response = http.post(
        `${API_URL}/api/companies`,
        JSON.stringify(companyData),
        { headers }
    );
    const endTime = Date.now();
    
    const success = check(response, {
        'POST /api/companies status is 200 or 201': (r) => r.status === 200 || r.status === 201,
        'POST /api/companies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
}

function testGetAuditLogs(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/auditlogs/legacy-view?pageNumber=1&pageSize=20`, { headers });
    const endTime = Date.now();
    
    const success = check(response, {
        'GET /api/auditlogs status is 200': (r) => r.status === 200,
        'GET /api/auditlogs has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(endTime - startTime);
}

// Teardown function - runs once after all VUs complete
export function teardown(data) {
    console.log('=== Load Test Complete ===');
    console.log('Check the summary report above for detailed metrics.');
    console.log('Key metrics to review:');
    console.log('  - api_response_time_ms (p99 should be <10ms)');
    console.log('  - http_req_duration (p99 should be <500ms)');
    console.log('  - error_rate (should be <1%)');
    console.log('  - requests_per_minute (should reach 10,000)');
    console.log('==========================');
}
