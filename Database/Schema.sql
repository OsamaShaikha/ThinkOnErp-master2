-- Schema for ThinkOn ERP Inventory Module
-- Core tables: Item, Warehouse, Bin, StockLedgerEntry, StockBalance, CostLayer, OpeningBalanceBatch

-- Item Master
CREATE TABLE Item (
    ItemId          VARCHAR2(30) PRIMARY KEY,
    ItemCode        VARCHAR2(30) NOT NULL UNIQUE,
    ItemName        VARCHAR2(200) NOT NULL,
    ItemType        VARCHAR2(20) CHECK (ItemType IN ('STOCK','NON_STOCK','SERVICE','KIT','ASSEMBLY')),
    UomBase         VARCHAR2(20) NOT NULL,
    UomConversions  CLOB, -- JSON mapping of UOM conversion factors
    CostingMethod   VARCHAR2(20) DEFAULT 'WeightedAverage',
    SerialTracking  CHAR(1) DEFAULT 'N', -- Y/N
    LotTracking      CHAR(1) DEFAULT 'N',
    ExpiryTracking   CHAR(1) DEFAULT 'N',
    GlControlAccount VARCHAR2(30),
    GlRevenueAccount VARCHAR2(30),
    GlCogsAccount    VARCHAR2(30),
    CreatedAt        TIMESTAMP DEFAULT SYSTIMESTAMP,
    UpdatedAt        TIMESTAMP DEFAULT SYSTIMESTAMP
);

-- Warehouse hierarchy
CREATE TABLE Warehouse (
    WarehouseId   VARCHAR2(30) PRIMARY KEY,
    BranchId      VARCHAR2(30) NOT NULL,
    WarehouseName VARCHAR2(200) NOT NULL,
    CreatedAt     TIMESTAMP DEFAULT SYSTIMESTAMP,
    UpdatedAt     TIMESTAMP DEFAULT SYSTIMESTAMP
);

CREATE TABLE Bin (
    BinId         VARCHAR2(30) PRIMARY KEY,
    WarehouseId   VARCHAR2(30) NOT NULL REFERENCES Warehouse(WarehouseId),
    ZoneType      VARCHAR2(20) CHECK (ZoneType IN ('STORAGE','RECEIVING','STAGING','QUARANTINE','DAMAGED','RETURNS')),
    CreatedAt     TIMESTAMP DEFAULT SYSTIMESTAMP,
    UpdatedAt     TIMESTAMP DEFAULT SYSTIMESTAMP
);

-- Stock Ledger (append‑only)
CREATE TABLE StockLedgerEntry (
    LedgerId        VARCHAR2(40) PRIMARY KEY,
    ItemId          VARCHAR2(30) NOT NULL REFERENCES Item(ItemId),
    WarehouseId     VARCHAR2(30) NOT NULL REFERENCES Warehouse(WarehouseId),
    BinId           VARCHAR2(30) REFERENCES Bin(BinId),
    TransactionDate TIMESTAMP NOT NULL,
    TransactionType VARCHAR2(20) NOT NULL,
    Quantity        NUMBER(14,4) NOT NULL CHECK (Quantity > 0),
    UnitCost        NUMBER(14,4) NOT NULL,
    TotalValue      NUMBER(18,4) GENERATED ALWAYS AS (Quantity * UnitCost) VIRTUAL,
    JournalEntryId  VARCHAR2(40),
    LotNumber       VARCHAR2(30),
    SerialNumber    VARCHAR2(30),
    LpnCode         VARCHAR2(30),
    CreatedAt       TIMESTAMP DEFAULT SYSTIMESTAMP
);

-- Cached balance per item/warehouse (quick read)
CREATE TABLE StockBalance (
    BalanceId       VARCHAR2(40) PRIMARY KEY,
    ItemId          VARCHAR2(30) NOT NULL REFERENCES Item(ItemId),
    WarehouseId     VARCHAR2(30) NOT NULL REFERENCES Warehouse(WarehouseId),
    OnHandQty       NUMBER(14,4) DEFAULT 0,
    ReservedQty     NUMBER(14,4) DEFAULT 0,
    AvailableQty    GENERATED ALWAYS AS (OnHandQty - ReservedQty) VIRTUAL,
    AvgCost         NUMBER(14,4) DEFAULT 0,
    UpdatedAt       TIMESTAMP DEFAULT SYSTIMESTAMP
);

-- Cost layering for FIFO/Lot handling
CREATE TABLE CostLayer (
    LayerId         VARCHAR2(40) PRIMARY KEY,
    ItemId          VARCHAR2(30) NOT NULL REFERENCES Item(ItemId),
    WarehouseId     VARCHAR2(30) NOT NULL REFERENCES Warehouse(WarehouseId),
    ReceivedQty     NUMBER(14,4) NOT NULL,
    RemainingQty    NUMBER(14,4) NOT NULL,
    UnitCost        NUMBER(14,4) NOT NULL,
    ReceivedDate    DATE NOT NULL,
    LotNumber       VARCHAR2(30),
    ExpiryDate      DATE,
    CreatedAt       TIMESTAMP DEFAULT SYSTIMESTAMP
);

-- Opening balance batch for initial import
CREATE TABLE OpeningBalanceBatch (
    BatchId         VARCHAR2(40) PRIMARY KEY,
    Description     VARCHAR2(200),
    ImportedAt      TIMESTAMP DEFAULT SYSTIMESTAMP,
    Status          VARCHAR2(20) CHECK (Status IN ('Pending','Completed','Failed')) DEFAULT 'Pending'
);

-- Indexes for performance
CREATE INDEX idx_stockledger_item_warehouse ON StockLedgerEntry (ItemId, WarehouseId, TransactionDate);
CREATE INDEX idx_stockbalance_item_warehouse ON StockBalance (ItemId, WarehouseId);
