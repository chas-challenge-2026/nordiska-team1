```json
{
  "openapi": "3.0.4",
  "info": {
    "title": "Nordiska.FrontendApi",
    "version": "1.0"
  },
  "paths": {
    "/api/auth/bankid/initiate": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdInitiateRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdInitiateRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdInitiateRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/auth/bankid/collect": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdCollectRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdCollectRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdCollectRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/auth/register": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RegisterCustomerRequestDto"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RegisterCustomerRequestDto"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RegisterCustomerRequestDto"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/auth/me": {
      "get": {
        "tags": [
          "Auth"
        ],
        "summary": "Returns the currently authenticated user's profile information extracted from the JWT.",
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/auth/logout": {
      "post": {
        "tags": [
          "Auth"
        ],
        "summary": "Logs out the user by clearing the HTTP-only authentication cookie.",
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ActiveLogin/BankId/Auth": {
      "get": {
        "tags": [
          "BankIdUiAuth"
        ],
        "parameters": [
          {
            "name": "returnUrl",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ActiveLogin/BankId/Auth/Api/Initialize": {
      "post": {
        "tags": [
          "BankIdUiAuthApi"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiInitializeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiInitializeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiInitializeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/BankIdUiApiInitializeResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/BankIdUiApiInitializeResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/BankIdUiApiInitializeResponse"
                }
              }
            }
          }
        }
      }
    },
    "/ActiveLogin/BankId/Auth/Api/Status": {
      "post": {
        "tags": [
          "BankIdUiAuthApi"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiStatusRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiStatusRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiStatusRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ActiveLogin/BankId/Auth/Api/QrCode": {
      "post": {
        "tags": [
          "BankIdUiAuthApi"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiQrCodeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiQrCodeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiQrCodeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ActiveLogin/BankId/Auth/Api/Cancel": {
      "post": {
        "tags": [
          "BankIdUiAuthApi"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiCancelRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiCancelRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BankIdUiApiCancelRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/faqs": {
      "post": {
        "tags": [
          "Faq"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateFaqRequest"
              }
            }
          }
        },
        "responses": {
          "201": {
            "description": "Created",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/FaqCreatedResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FaqCreatedResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FaqCreatedResponse"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "413": {
            "description": "Content Too Large",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "415": {
            "description": "Unsupported Media Type",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/api/faqs/{id}": {
      "delete": {
        "tags": [
          "Faq"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "maximum": 2147483647,
              "minimum": 1,
              "type": "integer",
              "format": "int32"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Faq"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "maximum": 2147483647,
              "minimum": 1,
              "type": "integer",
              "format": "int32"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/FaqEntryResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FaqEntryResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FaqEntryResponse"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ValidationProblemDetails"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/health/database": {
      "get": {
        "tags": [
          "Nordiska.FrontendApi"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/savingsaccounts": {
      "get": {
        "tags": [
          "SavingsAccounts"
        ],
        "summary": "Retrieves all savings accounts.",
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SavingsAccountResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SavingsAccountResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SavingsAccountResponse"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "SavingsAccounts"
        ],
        "summary": "Opens a new savings account.",
        "requestBody": {
          "description": "Open savings account request.",
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/OpenSavingsAccountRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/OpenSavingsAccountRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/OpenSavingsAccountRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SavingsAccountResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SavingsAccountResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SavingsAccountResponse"
                }
              }
            }
          }
        }
      }
    },
    "/api/savingsaccounts/{id}": {
      "get": {
        "tags": [
          "SavingsAccounts"
        ],
        "summary": "Retrieves a savings account by id.",
        "operationId": "GetSavingsAccountById",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "description": "Savings account id.",
            "required": true,
            "schema": {
              "type": "integer",
              "format": "int64"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SavingsAccountResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SavingsAccountResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SavingsAccountResponse"
                }
              }
            }
          }
        }
      }
    },
    "/api/test/secure": {
      "get": {
        "tags": [
          "Test"
        ],
        "summary": "Returns information extracted from the authenticated user's JWT claims.",
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/api/transactions": {
      "get": {
        "tags": [
          "Transactions"
        ],
        "summary": "Queries transactions, optionally filtered by account id.",
        "parameters": [
          {
            "name": "accountId",
            "in": "query",
            "description": "Optional account id to filter by.",
            "schema": {
              "type": "integer",
              "format": "int64"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/TransactionResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/TransactionResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/TransactionResponse"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Transactions"
        ],
        "summary": "Executes a transaction (deposit or withdrawal) on an account.",
        "requestBody": {
          "description": "Transaction request payload.",
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/TransactionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/TransactionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/TransactionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TransactionResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TransactionResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TransactionResponse"
                }
              }
            }
          }
        }
      }
    },
    "/api/transactions/{id}": {
      "get": {
        "tags": [
          "Transactions"
        ],
        "summary": "Retrieves a transaction by id.",
        "operationId": "GetTransactionById",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "description": "Transaction id.",
            "required": true,
            "schema": {
              "type": "integer",
              "format": "int64"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TransactionResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TransactionResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TransactionResponse"
                }
              }
            }
          }
        }
      }
    },
    "/api/transactions/balance/{accountId}": {
      "get": {
        "tags": [
          "Transactions"
        ],
        "summary": "Gets the current verified balance for an account computed from all ledger entries.",
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "description": "Account id.",
            "required": true,
            "schema": {
              "type": "integer",
              "format": "int64"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "number",
                  "format": "double"
                }
              },
              "application/json": {
                "schema": {
                  "type": "number",
                  "format": "double"
                }
              },
              "text/json": {
                "schema": {
                  "type": "number",
                  "format": "double"
                }
              }
            }
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "BankIdCollectRequest": {
        "type": "object",
        "properties": {
          "orderRef": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BankIdInitiateRequest": {
        "type": "object",
        "properties": {
          "personalNum": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BankIdUiApiCancelRequest": {
        "required": [
          "orderRef"
        ],
        "type": "object",
        "properties": {
          "orderRef": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "BankIdUiApiInitializeRequest": {
        "required": [
          "returnUrl"
        ],
        "type": "object",
        "properties": {
          "returnUrl": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "BankIdUiApiInitializeResponse": {
        "type": "object",
        "properties": {
          "isAutoLaunch": {
            "type": "boolean",
            "readOnly": true
          },
          "deviceMightRequireUserInteractionToLaunchBankIdApp": {
            "type": "boolean",
            "readOnly": true
          },
          "checkStatus": {
            "type": "boolean",
            "readOnly": true
          },
          "orderRef": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "redirectUri": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "qrStartState": {
            "type": "string",
            "nullable": true
          },
          "qrCodeAsBase64": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BankIdUiApiQrCodeRequest": {
        "required": [
          "qrStartState"
        ],
        "type": "object",
        "properties": {
          "qrStartState": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "BankIdUiApiStatusRequest": {
        "required": [
          "orderRef",
          "returnUrl"
        ],
        "type": "object",
        "properties": {
          "orderRef": {
            "minLength": 1,
            "type": "string"
          },
          "returnUrl": {
            "minLength": 1,
            "type": "string"
          },
          "autoStartAttempts": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "CreateFaqRequest": {
        "type": "object",
        "properties": {
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
        },
        "additionalProperties": false
      },
      "FaqCreatedResponse": {
        "type": "object",
        "properties": {
          "id": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "FaqEntryResponse": {
        "type": "object",
        "properties": {
          "id": {
            "type": "integer",
            "format": "int32"
          },
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
          "helpfulCount": {
            "type": "integer",
            "format": "int32"
          },
          "keywords": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "OpenSavingsAccountRequest": {
        "type": "object",
        "properties": {
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
        },
        "additionalProperties": false
      },
      "ProblemDetails": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "detail": {
            "type": "string",
            "nullable": true
          },
          "instance": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": {}
      },
      "RegisterCustomerRequestDto": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "email": {
            "type": "string",
            "nullable": true
          },
          "personalNum": {
            "type": "string",
            "nullable": true
          },
          "phoneNumber": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SavingsAccountResponse": {
        "type": "object",
        "properties": {
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
        },
        "additionalProperties": false
      },
      "TransactionRequest": {
        "type": "object",
        "properties": {
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
        },
        "additionalProperties": false
      },
      "TransactionResponse": {
        "type": "object",
        "properties": {
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
        },
        "additionalProperties": false
      },
      "ValidationProblemDetails": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "detail": {
            "type": "string",
            "nullable": true
          },
          "instance": {
            "type": "string",
            "nullable": true
          },
          "errors": {
            "type": "object",
            "additionalProperties": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "nullable": true
          }
        },
        "additionalProperties": {}
      }
    },
    "securitySchemes": {
      "Bearer": {
        "type": "http",
        "description": "Klistra in ditt JWT-token här (utan 'Bearer ' prefix).",
        "scheme": "Bearer",
        "bearerFormat": "JWT"
      }
    }
  },
  "security": [
    {}
  ],
  "tags": [
    {
      "name": "Auth"
    },
    {
      "name": "BankIdUiAuth"
    },
    {
      "name": "BankIdUiAuthApi"
    },
    {
      "name": "Faq"
    },
    {
      "name": "Nordiska.FrontendApi"
    },
    {
      "name": "SavingsAccounts"
    },
    {
      "name": "Test"
    },
    {
      "name": "Transactions"
    }
  ]
}
```
