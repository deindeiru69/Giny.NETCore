-- Migration 001 : Hero Mode — persistance des groupes de héros.
--
-- Tables :
--   hero_groups          : une row par groupe de héros (1:N avec accounts).
--   hero_group_members   : table de liaison groupe ↔ characters (N:M logique mais 1:1
--                          en pratique : un character appartient à 0 ou 1 groupe).
--
-- Notes Giny :
--   - L'ORM Giny ne supporte pas les composite primary keys ([Primary] est singulier
--     dans Giny.ORM.IO.DatabaseWriter). On ajoute donc un Id BIGINT sur
--     hero_group_members + UNIQUE KEY (GroupId, CharacterId) pour l'intégrité.
--   - Giny assigne les ids via UniqueLongIdProvider, mais AUTO_INCREMENT reste utile
--     pour les INSERT manuels (tests, scripts d'admin).

CREATE TABLE IF NOT EXISTS hero_groups (
    Id        BIGINT       NOT NULL AUTO_INCREMENT,
    AccountId INT          NOT NULL,
    LeaderId  BIGINT       NOT NULL,
    CreatedAt DATETIME     DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id),
    INDEX idx_account (AccountId),
    INDEX idx_leader  (LeaderId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS hero_group_members (
    Id          BIGINT   NOT NULL AUTO_INCREMENT,
    GroupId     BIGINT   NOT NULL,
    CharacterId BIGINT   NOT NULL,
    JoinOrder   TINYINT  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE KEY uk_group_char (GroupId, CharacterId),
    INDEX idx_group (GroupId),
    CONSTRAINT fk_group FOREIGN KEY (GroupId) REFERENCES hero_groups(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
