/**
 * Sustained Load Test Script for Full Traceability System (1 Hour)
 * 
 * Purpose: Validate that the traceability system maintains performance over extended periods
 * at the target load of 10,000 requests per minute for 1 hour.
 * 
 * Requirements:
 * - System SHALL support logging 10,000 requests per minute without degrading API response times
 * - System SHALL maintain performance over sustained periods (1 hour)
 * - System SHALL NOT show performance degradation (< 20% increase in latency over time)
 * - Memory usage SHALL remain stable (no memory leaks)
 * - Queue depth SHALL remain manageable (< 10,000 entries)
 * 
 * Prerequisites:
 * - k6 installed (https://k6.io/docs/getting-started/installation/)
 * - ThinkOnErp API running (default: http://localhost:5000)
 * - Valid JWT token for authentication
 * - Sufficient database capacity for ~600,000 audit log entries
 * 
 * Usage:
 *   k6 run sustained-load-test-1hour.js
 *   k6 run --env API_URL=http://your-api-url sustained-load-test-1hour.js
 *   k6 run --out json=sustained-results.json sustained-load-test-1hour.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';

// Custom metrics for sustained load validation
const auditOverhead = new Trend('audit_overhead_ms', true);
const apiResponseTime = new Trend('api_response_time_ms', true);
const errorRate = new Rate('error_rate');
const requestsPerMinute = new Counter('requests_per_minute');
const queueDepth = new Gauge('audit_queue_depth');
const memoryUsageMB = new Gauge('memory_usage_mb');

// Time-based metrics to track performance degradation
const responseTimeFirst10Min = new Trend('response_time_first_10min', true);
const responseTimeLast10Min = new Trend('response_time_last_10min', true);

// Test configuration
export const options = {
    // Sustained load scenario: 1 hour at 10,000 requests per minute
    scenarios: {
        sustained_load_1hour: {
            executor: 'ramping-arrival-rate',
            startRate: 100,  // Start with 100 requests per minute
            timeUnit: '1m',
            preAllocatedVUs: 100,
            maxVUs: 300,
            stages: [
                // Ramp up phase (10 minutes)
                { duration: '2m', target: 1000 },   // Ramp up to 1,000 req/min
                { duration: '3m', target: 5000 },   // Ramp up to 5,000 req/min
                { duration: '5m', target: 10000 },  // Ramp up to 10,000 req/min (target)
                
                // Sustained load phase (60 minutes at target)
                { duration: '60m', target: 10000 }, // Sustain 10,000 req/min for 1 HOUR
                
                // Ramp down phase (5 minutes)
                { duration: '3m', target: 1000 },   // Ramp down to 1,000 req/min
                { duration: '2m', target: 0 },      // Ramp down to 0
            ],
        },
    },
    
    // Performance thresholds (requirements validation)
    thresholds: {
        // Requirement: <10ms latency for 99% of operations (maintained throughout)
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
        
        // Error rate should be minimal throughout the test
        'error_rate': ['rate<0.01'],  // Less than 1% error rate
        
        // HTTP failures should be minimal
        'http_req_failed': ['rate<0.01'],
        
        // Performance degradation check
        // Last 10 minutes should not be more than 20% slower than first 10 minutes
        'response_time_last_10min': ['p(95)<1.2*response_time_first_10min.p(95)'],
    },
};

// Environment configuration
const API_URL = __ENV.API_URL || 'http://localhost:5000';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

// Track test start time for time-based metrics
let testStartTime = 0;

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

// Setup function - runs once per VU
export function setup() {
    console.log('=== Sustained Load Test Setup (1 Hour) ===');
    console.log(`API URL: ${API_URL}`);
    console.log(`Target: 10,000 requests per minute`);
    console.log(`Sustained Duration: 60 minutes`);
    console.log(`Total Duration: ~75 minutes (ramp up + sustained + ramp down)`);
    console.log(`Expected Total Requests: ~600,000`);
    console.log('==========================================');
    
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
    
    testStartTime = Date.now();
    
    return { token, testStartTime };
}

// Main test function - runs for each iteration
export default function(data) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
    };
    
    // Calculate elapsed time since test start
    const elapsedMinutes = (Date.now() - data.testStartTime) / 60000;
    
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
    
    // Execute the selected operation and track time-based metrics
    const responseTime = operations[selectedIndex](headers);
    
    // Track response times for first 10 minutes and last 10 minutes
    // to measure performance degradation
    if (elapsedMinutes >= 10 && elapsedMinutes <= 20) {
        // First 10 minutes of sustained load (after ramp-up)
        responseTimeFirst10Min.add(responseTime);
    } else if (elapsedMinutes >= 60 && elapsedMinutes <= 70) {
        // Last 10 minutes of sustained load
        responseTimeLast10Min.add(responseTime);
    }
    
    // Increment requests per minute counter
    requestsPerMinute.add(1);
    
    // Periodically check system health (every 100th request)
    if (Math.random() < 0.01) {
        checkSystemHealth(headers);
    }
    
    // Small random sleep to simulate realistic user behavior (0-100ms)
    sleep(Math.random() * 0.1);
}

// Test operations
function testGetCompanies(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/companies`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/companies status is 200': (r) => r.status === 200,
        'GET /api/companies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    // Check for audit overhead (if response includes timing headers)
    if (response.headers['X-Audit-Overhead-Ms']) {
        auditOverhead.add(parseFloat(response.headers['X-Audit-Overhead-Ms']));
    }
    
    return responseTime;
}

function testGetUsers(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/users`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/users status is 200': (r) => r.status === 200,
        'GET /api/users has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return responseTime;
}

function testGetRoles(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/roles`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/roles status is 200': (r) => r.status === 200,
        'GET /api/roles has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return responseTime;
}

function testGetCurrencies(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/currencies`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/currencies status is 200': (r) => r.status === 200,
        'GET /api/currencies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return responseTime;
}

function testGetBranches(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/branches`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/branches status is 200': (r) => r.status === 200,
        'GET /api/branches has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return responseTime;
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
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'POST /api/companies status is 200 or 201': (r) => r.status === 200 || r.status === 201,
        'POST /api/companies has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return responseTime;
}

function testGetAuditLogs(headers) {
    const startTime = Date.now();
    const response = http.get(`${API_URL}/api/auditlogs/legacy-view?pageNumber=1&pageSize=20`, { headers });
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    const success = check(response, {
        'GET /api/auditlogs status is 200': (r) => r.status === 200,
        'GET /api/auditlogs has correlation ID': (r) => r.headers['X-Correlation-Id'] !== undefined,
    });
    
    if (!success) {
        errorRate.add(1);
    } else {
        errorRate.add(0);
    }
    
    apiResponseTime.add(responseTime);
    
    return responseTime;
}

// Check system health metrics
function checkSystemHealth(headers) {
    const response = http.get(`${API_URL}/api/monitoring/health`, { headers });
    
    if (response.status === 200) {
        try {
            const healthData = JSON.parse(response.body);
            
            // Track queue depth if available
            if (healthData.auditQueueDepth !== undefined) {
                queueDepth.add(healthData.auditQueueDepth);
            }
            
            // Track memory usage if available
            if (healthData.memoryUsageMB !== undefined) {
                memoryUsageMB.add(healthData.memoryUsageMB);
            }
            
            // Log warnings if thresholds are exceeded
            if (healthData.auditQueueDepth > 10000) {
                console.warn(`WARNING: Audit queue depth exceeded 10,000: ${healthData.auditQueueDepth}`);
            }
        } catch (e) {
            // Health endpoint might not return JSON or might not be available
            // This is optional monitoring, so we don't fail the test
        }
    }
}

// Teardown function - runs once after all VUs complete
export function teardown(data) {
    console.log('=== Sustained Load Test Complete (1 Hour) ===');
    console.log('Test Duration: ~75 minutes');
    console.log('Sustained Load Duration: 60 minutes at 10,000 req/min');
    console.log('Expected Total Requests: ~600,000');
    console.log('');
    console.log('Key metrics to review:');
    console.log('  - api_response_time_ms (p99 should be <10ms throughout)');
    console.log('  - http_req_duration (p99 should be <500ms throughout)');
    console.log('  - error_rate (should be <1% throughout)');
    console.log('  - response_time_first_10min vs response_time_last_10min');
    console.log('    (degradation should be <20%)');
    console.log('  - audit_queue_depth (should stay <10,000)');
    console.log('  - memory_usage_mb (should remain stable, no leaks)');
    console.log('');
    console.log('Performance Degradation Analysis:');
    console.log('  Compare p95 of first 10 minutes vs last 10 minutes');
    console.log('  Acceptable: <20% increase');
    console.log('  Warning: 20-50% increase');
    console.log('  Critical: >50% increase');
    console.log('==============================================');
}
