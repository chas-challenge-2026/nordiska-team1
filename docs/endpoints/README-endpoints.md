# Nordiska API

Alla endpoints. 

 
 [Hela scalar i rå json](Scalar-json.json)

 [Hela scalar i markdown](Scalar-markdown.md)

 [Sparad html](Scalar-markdown.md)



## API Map

| Område | Endpoints |
|---|---:|
| [ Auth](#auth) | 5 |
| [ BankID UI](#bankid-ui) | 1 |
| [ BankID UI API](#bankid-ui-api) | 4 |
| [ FAQ](#faq) | 3 |
| [ Health](#health) | 1 |
| [ Savings Accounts](#savings-accounts) | 3 |
| [ Test](#test) | 1 |
| [Transactions](#transactions) | 4 |

## Endpoint Map

###  Auth

- [`POST /api/auth/bankid/initiate`](#post-apiauthbankidinitiate)
- [`POST /api/auth/bankid/collect`](#post-apiauthbankidcollect)
- [`POST /api/auth/register`](#post-apiauthregister)
- [`GET /api/auth/me`](#get-apiauthme)
- [`POST /api/auth/logout`](#post-apiauthlogout)

###  BankID UI

- [`GET /ActiveLogin/BankId/Auth`](#get-activeloginbankidauth)

###  BankID UI API

- [`POST /ActiveLogin/BankId/Auth/Api/Initialize`](#post-activeloginbankidauthapiinitialize)
- [`POST /ActiveLogin/BankId/Auth/Api/Status`](#post-activeloginbankidauthapistatus)
- [`POST /ActiveLogin/BankId/Auth/Api/QrCode`](#post-activeloginbankidauthapiqrcode)
- [`POST /ActiveLogin/BankId/Auth/Api/Cancel`](#post-activeloginbankidauthapicancel)

###  FAQ

- [`POST /api/faqs`](#post-apifaqs)
- [`DELETE /api/faqs/{id}`](#delete-apifaqsid)
- [`GET /api/faqs/{id}`](#get-apifaqsid)

###  Health

- [`GET /health/database`](#get-healthdatabase)

###  Savings Accounts

- [`GET /api/savingsaccounts`](#get-apisavingsaccounts)
- [`POST /api/savingsaccounts`](#post-apisavingsaccounts)
- [`GET /api/savingsaccounts/{id}`](#get-apisavingsaccountsid)

###  Test

- [`GET /api/test/secure`](#get-apitestsecure)

###  Transactions

- [`GET /api/transactions`](#get-apitransactions)
- [`POST /api/transactions`](#post-apitransactions)
- [`GET /api/transactions/{id}`](#get-apitransactionsid)
- [`GET /api/transactions/balance/{accountId}`](#get-apitransactionsbalanceaccountid)

---

## Auth

### POST /api/auth/bankid/initiate

```json
{
  "method": "POST",
  "path": "/api/auth/bankid/initiate",
  "auth": false,
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "personalNum": {
          "type": "string",
          "nullable": true
        }
      }
    }
  },
  "output": {
    "200": {
      "body": "UNKNOWN",
      "description": "BankID initiation succeeded."
    }
  },
  "errors": {
    "400": {
      "body": {
        "type": "object",
        "fields": {
          "message": {
            "type": "string"
          }
        }
      },
      "example": {
        "message": "<error message>"
      },
      "description": "BankID initiation failed."
    }
  }
}
```

### POST /api/auth/bankid/collect

```json
{
  "method": "POST",
  "path": "/api/auth/bankid/collect",
  "auth": false,
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "orderRef": {
          "type": "string",
          "nullable": true
        }
      }
    }
  },
  "output": {
    "200": {
      "body": "UNKNOWN",
      "description": "BankID collection/authentication succeeded."
    }
  },
  "errors": {
    "401": {
      "body": {
        "type": "object",
        "fields": {
          "message": {
            "type": "string"
          }
        }
      },
      "example": {
        "message": "<error message>"
      },
      "description": "BankID collection/authentication failed."
    }
  }
}
```

### POST /api/auth/register

```json
{
  "method": "POST",
  "path": "/api/auth/register",
  "auth": false,
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "name": {
          "type": "string",
          "required": true
        },
        "email": {
          "type": "string",
          "required": true
        },
        "personalNum": {
          "type": "string",
          "required": true
        },
        "phoneNumber": {
          "type": "string",
          "required": true
        }
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "token": {
            "type": "string"
          }
        }
      },
      "example": {
        "token": "<jwt token>"
      },
      "description": "Customer registered successfully."
    }
  },
  "errors": {
    "400": {
      "body": {
        "type": "object",
        "fields": {
          "message": {
            "type": "string"
          },
          "errors": {
            "type": "UNKNOWN",
            "nullable": true
          }
        }
      },
      "description": "Registration failed."
    }
  }
}
```

### GET /api/auth/me

```json
{
  "method": "GET",
  "path": "/api/auth/me",
  "auth": true,
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "id": {
            "type": "string",
            "nullable": true
          },
          "email": {
            "type": "string",
            "nullable": true
          },
          "role": {
            "type": "string",
            "nullable": true
          }
        }
      },
      "example": {
        "id": "123",
        "email": "user@example.com",
        "role": "Customer"
      },
      "description": "Currently authenticated user's JWT-derived profile."
    }
  },
  "errors": {
    "401": {
      "description": "Missing or invalid authentication."
    }
  }
}
```

### POST /api/auth/logout

```json
{
  "method": "POST",
  "path": "/api/auth/logout",
  "auth": false,
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "message": {
            "type": "string"
          }
        }
      },
      "example": {
        "message": "Logged out successfully"
      },
      "description": "Logout completed."
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## BankID UI

### GET /ActiveLogin/BankId/Auth

```json
{
  "method": "GET",
  "path": "/ActiveLogin/BankId/Auth",
  "auth": "UNKNOWN",
  "input": {
    "query": {
      "returnUrl": {
        "type": "string"
      }
    }
  },
  "output": {
    "200": {
      "description": "OK"
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## BankID UI API

### POST /ActiveLogin/BankId/Auth/Api/Initialize

```json
{
  "method": "POST",
  "path": "/ActiveLogin/BankId/Auth/Api/Initialize",
  "auth": "UNKNOWN",
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "returnUrl": {
          "type": "string",
          "required": true,
          "minLength": 1
        }
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "isAutoLaunch": {
            "type": "boolean"
          },
          "deviceMightRequireUserInteractionToLaunchBankIdApp": {
            "type": "boolean"
          },
          "checkStatus": {
            "type": "boolean"
          },
          "orderRef": {
            "type": "string",
            "nullable": true
          },
          "redirectUri": {
            "type": "string",
            "nullable": true
          },
          "qrStartState": {
            "type": "string",
            "nullable": true
          },
          "qrCodeAsBase64": {
            "type": "string",
            "nullable": true
          }
        }
      }
    }
  }
}
```

### POST /ActiveLogin/BankId/Auth/Api/Status

```json
{
  "method": "POST",
  "path": "/ActiveLogin/BankId/Auth/Api/Status",
  "auth": "UNKNOWN",
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "orderRef": {
          "type": "string",
          "required": true,
          "minLength": 1
        },
        "returnUrl": {
          "type": "string",
          "required": true,
          "minLength": 1
        },
        "autoStartAttempts": {
          "type": "integer",
          "format": "int32"
        }
      }
    }
  },
  "output": {
    "200": {
      "description": "OK"
    }
  }
}
```

### POST /ActiveLogin/BankId/Auth/Api/QrCode

```json
{
  "method": "POST",
  "path": "/ActiveLogin/BankId/Auth/Api/QrCode",
  "auth": "UNKNOWN",
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "qrStartState": {
          "type": "string",
          "required": true,
          "minLength": 1
        }
      }
    }
  },
  "output": {
    "200": {
      "description": "OK"
    }
  }
}
```

### POST /ActiveLogin/BankId/Auth/Api/Cancel

```json
{
  "method": "POST",
  "path": "/ActiveLogin/BankId/Auth/Api/Cancel",
  "auth": "UNKNOWN",
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "orderRef": {
          "type": "string",
          "required": true,
          "minLength": 1
        }
      }
    }
  },
  "output": {
    "200": {
      "description": "OK"
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## FAQ

### POST /api/faqs

```json
{
  "method": "POST",
  "path": "/api/faqs",
  "auth": false,
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "question": {
          "type": "string",
          "nullable": true
        },
        "answer": {
          "type": "string",
          "nullable": true
        },
        "category": {
          "type": "string",
          "nullable": true
        },
        "keywords": {
          "type": "string",
          "nullable": true
        }
      }
    }
  },
  "output": {
    "201": {
      "body": {
        "type": "FaqCreatedResponse"
      },
      "description": "FAQ created."
    }
  },
  "errors": {
    "400": {
      "body": {
        "type": "ValidationProblemDetails"
      },
      "description": "Validation failed."
    },
    "401": {
      "description": "Unauthorized."
    },
    "403": {
      "description": "Forbidden."
    },
    "413": {
      "description": "Payload too large."
    },
    "415": {
      "description": "Unsupported media type."
    },
    "500": {
      "body": {
        "type": "ProblemDetails"
      },
      "description": "Internal server error."
    }
  }
}
```

### DELETE /api/faqs/{id}

```json
{
  "method": "DELETE",
  "path": "/api/faqs/{id}",
  "auth": false,
  "input": {
    "path": {
      "id": {
        "type": "integer",
        "format": "int32",
        "required": true
      }
    }
  },
  "output": {
    "204": {
      "description": "FAQ deleted successfully."
    }
  },
  "errors": {
    "400": {
      "body": {
        "type": "ValidationProblemDetails"
      },
      "description": "Invalid id."
    },
    "401": {
      "description": "Unauthorized."
    },
    "403": {
      "description": "Forbidden."
    },
    "404": {
      "body": {
        "type": "ProblemDetails"
      },
      "example": {
        "title": "FAQ entry not found.",
        "status": 404
      },
      "description": "FAQ entry not found."
    },
    "500": {
      "body": {
        "type": "ProblemDetails"
      },
      "description": "Internal server error."
    }
  }
}
```

### GET /api/faqs/{id}

```json
{
  "method": "GET",
  "path": "/api/faqs/{id}",
  "auth": false,
  "input": {
    "path": {
      "id": {
        "type": "integer",
        "format": "int32",
        "required": true
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "FaqEntryResponse"
      },
      "description": "FAQ entry returned."
    }
  },
  "errors": {
    "400": {
      "body": {
        "type": "ValidationProblemDetails"
      },
      "description": "Invalid id."
    },
    "401": {
      "description": "Unauthorized."
    },
    "403": {
      "description": "Forbidden."
    },
    "404": {
      "body": {
        "type": "ProblemDetails"
      },
      "example": {
        "title": "FAQ entry not found.",
        "status": 404
      },
      "description": "FAQ entry not found."
    },
    "500": {
      "body": {
        "type": "ProblemDetails"
      },
      "description": "Internal server error."
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## Health

### GET /health/database

```json
{
  "method": "GET",
  "path": "/health/database",
  "auth": "UNKNOWN",
  "output": {
    "200": {
      "description": "OK"
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## Savings Accounts

### GET /api/savingsaccounts

```json
{
  "method": "GET",
  "path": "/api/savingsaccounts",
  "auth": "UNKNOWN",
  "output": {
    "200": {
      "body": {
        "type": "array",
        "items": {
          "type": "object",
          "fields": {
            "id": {
              "type": "integer",
              "format": "int64"
            },
            "customerId": {
              "type": "integer",
              "format": "int64"
            },
            "accountNumber": {
              "type": "string",
              "nullable": true
            },
            "accountType": {
              "type": "string",
              "nullable": true
            },
            "balance": {
              "type": "number",
              "format": "double"
            },
            "interestRate": {
              "type": "number",
              "format": "double"
            },
            "createdAt": {
              "type": "string",
              "format": "date-time"
            }
          }
        }
      }
    }
  }
}
```

### POST /api/savingsaccounts

```json
{
  "method": "POST",
  "path": "/api/savingsaccounts",
  "auth": "UNKNOWN",
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "customerId": {
          "type": "integer",
          "format": "int64"
        },
        "accountNumber": {
          "type": "string",
          "nullable": true
        },
        "accountType": {
          "type": "string",
          "nullable": true
        },
        "initialDeposit": {
          "type": "number",
          "format": "double"
        },
        "interestRate": {
          "type": "number",
          "format": "double"
        }
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "customerId": {
            "type": "integer",
            "format": "int64"
          },
          "accountNumber": {
            "type": "string",
            "nullable": true
          },
          "accountType": {
            "type": "string",
            "nullable": true
          },
          "balance": {
            "type": "number",
            "format": "double"
          },
          "interestRate": {
            "type": "number",
            "format": "double"
          },
          "createdAt": {
            "type": "string",
            "format": "date-time"
          }
        }
      }
    }
  }
}
```

### GET /api/savingsaccounts/{id}

```json
{
  "method": "GET",
  "path": "/api/savingsaccounts/{id}",
  "auth": "UNKNOWN",
  "input": {
    "path": {
      "id": {
        "type": "integer",
        "format": "int64",
        "required": true,
        "description": "Savings account id."
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "customerId": {
            "type": "integer",
            "format": "int64"
          },
          "accountNumber": {
            "type": "string",
            "nullable": true
          },
          "accountType": {
            "type": "string",
            "nullable": true
          },
          "balance": {
            "type": "number",
            "format": "double"
          },
          "interestRate": {
            "type": "number",
            "format": "double"
          },
          "createdAt": {
            "type": "string",
            "format": "date-time"
          }
        }
      }
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## Test

### GET /api/test/secure

```json
{
  "method": "GET",
  "path": "/api/test/secure",
  "auth": true,
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "message": {
            "type": "string"
          },
          "customerId": {
            "type": "string",
            "nullable": true
          },
          "email": {
            "type": "string",
            "nullable": true
          },
          "role": {
            "type": "string",
            "nullable": true
          },
          "allClaims": {
            "type": "array",
            "items": {
              "type": "object",
              "fields": {
                "type": {
                  "type": "string"
                },
                "value": {
                  "type": "string"
                }
              }
            }
          }
        }
      },
      "example": {
        "message": "You have successfully accessed a protected endpoint with a valid JWT!",
        "customerId": "123",
        "email": "user@example.com",
        "role": "Customer",
        "allClaims": [
          {
            "type": "<claim type>",
            "value": "<claim value>"
          }
        ]
      },
      "description": "Authenticated JWT diagnostics."
    }
  },
  "errors": {
    "401": {
      "description": "Missing or invalid authentication."
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## Transactions

### GET /api/transactions

```json
{
  "method": "GET",
  "path": "/api/transactions",
  "auth": "UNKNOWN",
  "input": {
    "query": {
      "accountId": {
        "type": "integer",
        "format": "int64",
        "description": "Optional account id to filter by."
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "array",
        "items": {
          "type": "object",
          "fields": {
            "id": {
              "type": "integer",
              "format": "int64"
            },
            "accountId": {
              "type": "integer",
              "format": "int64"
            },
            "type": {
              "type": "string",
              "nullable": true
            },
            "amount": {
              "type": "number",
              "format": "double"
            },
            "createdAt": {
              "type": "string",
              "format": "date-time"
            }
          }
        }
      }
    }
  }
}
```

### POST /api/transactions

```json
{
  "method": "POST",
  "path": "/api/transactions",
  "auth": "UNKNOWN",
  "input": {
    "body": {
      "type": "object",
      "fields": {
        "accountId": {
          "type": "integer",
          "format": "int64"
        },
        "type": {
          "type": "string",
          "nullable": true
        },
        "amount": {
          "type": "number",
          "format": "double"
        }
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "accountId": {
            "type": "integer",
            "format": "int64"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "amount": {
            "type": "number",
            "format": "double"
          },
          "createdAt": {
            "type": "string",
            "format": "date-time"
          }
        }
      }
    }
  }
}
```

### GET /api/transactions/{id}

```json
{
  "method": "GET",
  "path": "/api/transactions/{id}",
  "auth": "UNKNOWN",
  "input": {
    "path": {
      "id": {
        "type": "integer",
        "format": "int64",
        "required": true,
        "description": "Transaction id."
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "object",
        "fields": {
          "id": {
            "type": "integer",
            "format": "int64"
          },
          "accountId": {
            "type": "integer",
            "format": "int64"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "amount": {
            "type": "number",
            "format": "double"
          },
          "createdAt": {
            "type": "string",
            "format": "date-time"
          }
        }
      }
    }
  }
}
```

### GET /api/transactions/balance/{accountId}

```json
{
  "method": "GET",
  "path": "/api/transactions/balance/{accountId}",
  "auth": "UNKNOWN",
  "input": {
    "path": {
      "accountId": {
        "type": "integer",
        "format": "int64",
        "required": true,
        "description": "Account id."
      }
    }
  },
  "output": {
    "200": {
      "body": {
        "type": "number",
        "format": "double"
      }
    }
  }
}
```

[↑ Till API Map](#api-map)

---

## Fält

- `method` – HTTP-metod.
- `path` – URL-path.
- `auth` – `true`, `false` eller `"UNKNOWN"`.
- `input.path` – värden som ligger i URL:en.
- `input.query` – query-parametrar.
- `input.body` – JSON som frontend skickar.
- `output` – dokumenterade success-responses.
- `errors` – dokumenterade error-responses.

`"UNKNOWN"` betyder att backendkoden eller nuvarande OpenAPI-dokumentation inte gav tillräcklig information. Ingenting har gissats.
